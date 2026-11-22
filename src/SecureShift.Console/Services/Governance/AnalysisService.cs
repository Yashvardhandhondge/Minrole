using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Context.Generated;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Services.Governance;

/// <summary>
/// Step 3 "Run" of the Analysis Wizard: scores every identity of the 3 files and saves the whole result
/// (Analyses, Identities, IdentityScores, PermissionUsage, Findings, GeneratedRoles + actions,
/// Notifications, AuditLogs) in ONE transaction: either everything is saved or nothing is.
/// </summary>
public sealed class AnalysisService(IDbContextFactory<SecureShiftDbContext> factory, TimeProvider clock) : ServiceBase(factory, clock)
{
    /// <summary>The 6 stages shown while the analysis runs (analysis.js runAnalysis()).</summary>
    public static readonly string[] Stages =
    [
        "Data Processing & Validation", "Identity & Permission Analysis", "Usage & Telemetry Assessment",
        "Over-Privilege & Evidence Scoring", "Recommendation Engine (least-privilege roles)", "Audit & Reporting",
    ];

    /// <summary>Rule settings used when the table has no active row (the prototype's rules.json defaults).</summary>
    public static RuleSetting DefaultRules() => new()
    {
        WeightUnused = 50, WeightHighRisk = 30, WeightScope = 20, DefaultPeriodDays = 90, RareThreshold = 3,
        IncludeBaselineRead = true, IsActive = true,
    };

    public async Task<Analysis> RunAsync(int companyId, GovernanceInput input, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var company = await db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId) ?? throw new UserFacingException("Company not found.");
        var rules = await db.RuleSettings.AsNoTracking().Where(r => r.IsActive).OrderByDescending(r => r.RuleSettingId).FirstOrDefaultAsync() ?? DefaultRules();
        var patterns = await db.HighRiskPatterns.AsNoTracking().Where(p => p.IsEnabled).Select(p => p.Pattern).ToListAsync();
        var now = UtcNow;

        var analysis = new Analysis
        {
            Code = await NextCodeAsync(db),
            CompanyId = company.CompanyId,
            PeriodFrom = input.PeriodFrom,
            PeriodTo = input.PeriodTo,
            Status = "Complete",
            RunBy = actorId,
            RunAt = now,
        };
        db.Analyses.Add(analysis);

        var existing = await db.Identities.Where(i => i.CompanyId == company.CompanyId).ToListAsync();
        var scores = new List<int>();
        var findingCount = 0;
        var roleCount = 0;

        foreach (var ra in input.Assignments)
        {
            // ----- identity (one row per company + user name, kept up to date) -----
            var identity = existing.FirstOrDefault(i => i.UserName.Equals(ra.UserName, StringComparison.OrdinalIgnoreCase));
            if (identity is null)
            {
                identity = new Identity { CompanyId = company.CompanyId, UserName = ra.UserName };
                db.Identities.Add(identity);
                existing.Add(identity);
            }
            identity.DisplayName = ra.DisplayName;
            identity.IdentityType = ra.IdentityType;
            identity.AssignedRole = GovernanceInputReader.FindRole(input.Roles, ra)!.RoleName;
            identity.Scope = ra.Scope;
            identity.ScopeName = ra.ScopeName;

            // ----- granted vs used -----
            var granted = GovernanceInputReader.FindRole(input.Roles, ra)!.Actions;
            var grantedSet = new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase);
            var mine = input.Events.Where(ev => ev.Caller.Equals(ra.PrincipalId, StringComparison.OrdinalIgnoreCase)
                                             || ev.Caller.Equals(ra.PrincipalName, StringComparison.OrdinalIgnoreCase)
                                             || ev.Caller.Equals(ra.UserName, StringComparison.OrdinalIgnoreCase))
                                   .Where(ev => grantedSet.Contains(ev.Action)).ToList();
            var usage = mine.GroupBy(ev => granted.First(g => g.Equals(ev.Action, StringComparison.OrdinalIgnoreCase)))
                .Select(g => new
                {
                    Action = g.Key,
                    First = DateOnly.FromDateTime(g.Min(x => x.Timestamp).UtcDateTime),
                    Last = DateOnly.FromDateTime(g.Max(x => x.Timestamp).UtcDateTime),
                    Count = g.Count(),
                    Rg = g.Where(x => x.ResourceGroup is not null).GroupBy(x => x.ResourceGroup!).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).Select(x => x.Key).FirstOrDefault(),
                }).OrderBy(u => u.Action, StringComparer.OrdinalIgnoreCase).ToList();
            var usedSet = new HashSet<string>(usage.Select(u => u.Action), StringComparer.OrdinalIgnoreCase);
            var unusedHighRisk = granted.Where(a => !usedSet.Contains(a) && patterns.Any(p => GovernanceScoring.Matches(p, a))).ToList();

            var score = GovernanceScoring.Score(granted.Count, usage.Count, unusedHighRisk.Count, ra.Scope, rules.WeightUnused, rules.WeightHighRisk, rules.WeightScope);
            scores.Add(score);
            var suggested = mine.Where(x => x.ResourceGroup is not null).GroupBy(x => x.ResourceGroup!)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).FirstOrDefault();

            db.IdentityScores.Add(new IdentityScore
            {
                Analysis = analysis, Identity = identity, GrantedCount = granted.Count, UsedCount = usage.Count,
                UnusedHighRisk = unusedHighRisk.Count, RiskScore = score, SuggestedScope = suggested,
            });
            foreach (var u in usage)
                db.PermissionUsages.Add(new PermissionUsage
                {
                    Analysis = analysis, Identity = identity, Action = u.Action, FirstUsed = u.First, LastUsed = u.Last, EventCount = u.Count, ResourceGroup = u.Rg,
                });

            // ----- findings: unused high-risk actions (prototype categories only) -----
            foreach (var action in unusedHighRisk)
            {
                var category = GovernanceScoring.CategoryOf(action);
                if (category is null) continue;   // counted in the score, but not one of the 4 finding categories
                db.Findings.Add(new Finding
                {
                    Analysis = analysis, Identity = identity, Action = action, Category = category,
                    Severity = GovernanceScoring.SeverityOf(category, ra.Scope), GrantedByRole = identity.AssignedRole, LastUsed = null, Status = "Open",
                });
                findingCount++;
            }

            // ----- least-privilege role: only for over-privileged identities that used something -----
            if (usage.Count > 0 && usage.Count < granted.Count)
            {
                var role = new GeneratedRole
                {
                    Analysis = analysis, Identity = identity, RoleName = $"{identity.UserName}-custom-role",
                    OriginalRole = $"{identity.AssignedRole} ({identity.ScopeName ?? identity.Scope})",
                    AssignableScope = suggested ?? identity.ScopeName ?? identity.Scope,
                    IncludeBaselineRead = rules.IncludeBaselineRead, IsReviewed = false,
                };
                foreach (var u in usage)
                {
                    var rare = u.Count < rules.RareThreshold;
                    role.GeneratedRoleActions.Add(new GeneratedRoleAction
                    {
                        Action = u.Action, IsRare = rare,
                        Note = rare ? GovernanceScoring.RareNote(u.Count, input.PeriodTo.DayNumber - u.Last.DayNumber) : null,
                    });
                }
                db.GeneratedRoles.Add(role);
                roleCount++;
            }
        }

        analysis.IdentityCount = scores.Count;
        analysis.AvgScore = scores.Count == 0 ? 0 : (decimal)JsMath.Round2(scores.Average());
        analysis.HighRiskCount = findingCount;

        // prototype notification texts (governance-data.js SS.GOV.notifications)
        foreach (var message in new[]
                 {
                     $"Analysis {analysis.Code} completed for {company.CompanyName}",
                     $"{roleCount} generated roles are waiting for review",
                     $"{findingCount} new high-risk unused permissions found",
                 })
            db.Notifications.Add(new Notification { UserId = actorId, Message = message, IsRead = false, CreatedAt = now });

        Audit.Add(db, actorId, "Analysis Run", analysis.Code,
            $"{company.CompanyName} · {Fmt.Date(input.PeriodFrom)} - {Fmt.Date(input.PeriodTo)} · {scores.Count} identities · avg {Fmt.Dec2(analysis.AvgScore.Value)}", now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return analysis;
    }

    /// <summary>analysis.js: "AN-2026-" + (1111 + number of analyses); the year is the current year. Skips codes already used.</summary>
    private async Task<string> NextCodeAsync(SecureShiftDbContext db)
    {
        var n = 1111 + await db.Analyses.CountAsync();
        while (true)
        {
            var code = $"AN-{Today.Year}-{n}";
            if (!await db.Analyses.AnyAsync(a => a.Code == code)) return code;
            n++;
        }
    }
}
