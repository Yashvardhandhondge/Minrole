using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Context.Generated;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Services.Governance;

/// <summary>Numbers of the Governance dashboard (governance/js/dashboard.js). Not an entity.</summary>
public sealed record GovernanceDashboard(Analysis? Latest, int OpenFindings, int PendingRoles, int High, int Medium, int Low,
    IReadOnlyList<VwIdentityRisk> Top5, IReadOnlyList<Analysis> Trend);

/// <summary>
/// Cloud Admin screens except the analysis run: companies, identities, findings, generated roles,
/// rule settings, high-risk patterns, reports, notifications (governance/js/*.js).
/// </summary>
public sealed partial class GovernanceService(IDbContextFactory<SecureShiftDbContext> factory, TimeProvider clock) : ServiceBase(factory, clock)
{
    public static readonly string[] Categories = ["Delete", "Network", "Key Vault", "Role Assignment"];
    public static readonly string[] IdentityTypes = ["User", "Service Principal", "Managed Identity"];
    public static readonly int[] PeriodPresets = [30, 60, 90, 180];   // rules.html "Default analysis window"

    [GeneratedRegex(@"^[A-Za-z0-9./*-]+$")] private static partial Regex PatternRx();

    /* ------------------------------------------------------------------ companies */

    public async Task<List<Company>> CompaniesAsync()
    {
        await using var db = await NewDbAsync();
        return await db.Companies.AsNoTracking().Include(c => c.Analyses).OrderBy(c => c.CompanyId).ToListAsync();
    }

    /// <summary>The latest analysis of a company (newest RunAt), or null = "Not analysed".</summary>
    public static Analysis? LatestOf(Company c) => c.Analyses.OrderByDescending(a => a.RunAt).ThenByDescending(a => a.AnalysisId).FirstOrDefault();

    /// <summary>companies.js "Add Company Data" with the prototype messages.</summary>
    public async Task<Company> AddCompanyAsync(string name, string tenantId, string subscriptions, int actorId)
    {
        name = name.Trim();
        tenantId = tenantId.Trim();
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var lower = name.ToLower();
        var exists = await db.Companies.AnyAsync(c => c.CompanyName.ToLower() == lower);
        Validation.ThrowIfAny(
            ("Company name", name.Length < 3 ? "Enter at least 3 characters" : name.Length > 100 ? "Max 100 characters" : exists ? "This company already exists" : ""),
            ("Tenant ID", tenantId.Length < 8 ? "Enter a valid tenant ID" : tenantId.Length > 36 ? "Max 36 characters (a GUID)" : ""),
            ("Subscriptions", Validation.IsInt(subscriptions, 1, 500) ? "" : "At least 1 (max 500)"));
        var company = new Company
        {
            CompanyName = name, TenantId = tenantId, Subscriptions = int.Parse(subscriptions, CultureInfo.InvariantCulture),
            CreatedBy = actorId, CreatedAt = UtcNow,
        };
        db.Companies.Add(company);
        Audit.Add(db, actorId, "Company Added", name, $"Tenant {tenantId} · {company.Subscriptions} subscription(s)", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return company;
    }

    /* ------------------------------------------------------------------ analyses + dashboard */

    public async Task<List<Analysis>> AnalysesAsync()
    {
        await using var db = await NewDbAsync();
        return await db.Analyses.AsNoTracking().Include(a => a.Company).Include(a => a.RunByNavigation)
            .OrderByDescending(a => a.RunAt).ThenByDescending(a => a.AnalysisId).ToListAsync();
    }

    public async Task<Analysis?> AnalysisAsync(int analysisId)
    {
        await using var db = await NewDbAsync();
        return await db.Analyses.AsNoTracking().Include(a => a.Company).FirstOrDefaultAsync(a => a.AnalysisId == analysisId);
    }

    public async Task<GovernanceDashboard> DashboardAsync()
    {
        await using var db = await NewDbAsync();
        var latest = await db.Analyses.AsNoTracking().Include(a => a.Company).OrderByDescending(a => a.RunAt).ThenByDescending(a => a.AnalysisId).FirstOrDefaultAsync();
        if (latest is null) return new GovernanceDashboard(null, 0, 0, 0, 0, 0, [], []);
        var scores = await db.VwIdentityRisks.AsNoTracking().Where(v => v.AnalysisId == latest.AnalysisId).ToListAsync();
        var open = await db.Findings.CountAsync(f => f.AnalysisId == latest.AnalysisId && f.Status == "Open");
        var pending = await db.GeneratedRoles.CountAsync(r => r.AnalysisId == latest.AnalysisId && !r.IsReviewed);
        var trend = await db.Analyses.AsNoTracking().Where(a => a.CompanyId == latest.CompanyId).OrderByDescending(a => a.RunAt).Take(6).ToListAsync();
        trend.Reverse();
        return new GovernanceDashboard(latest, open, pending,
            scores.Count(s => s.RiskScore >= 70), scores.Count(s => s.RiskScore is >= 40 and < 70), scores.Count(s => s.RiskScore < 40),
            scores.OrderByDescending(s => s.RiskScore).ThenBy(s => s.DisplayName).Take(5).ToList(), trend);
    }

    /* ------------------------------------------------------------------ identities */

    /// <summary>Identities page: dev."vw_IdentityRisk" of one analysis, highest score first, with the prototype filters.</summary>
    public async Task<List<VwIdentityRisk>> IdentitiesAsync(int analysisId, string? search, string? type, string? risk)
    {
        await using var db = await NewDbAsync();
        var q = db.VwIdentityRisks.AsNoTracking().Where(v => v.AnalysisId == analysisId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(v => (v.DisplayName + " " + v.UserName + " " + v.AssignedRole + " " + (v.ScopeName ?? v.Scope)).ToLower().Contains(s));
        }
        if (!string.IsNullOrEmpty(type)) q = q.Where(v => v.IdentityType == type);
        if (!string.IsNullOrEmpty(risk)) q = q.Where(v => v.RiskLevel == risk);
        return await q.OrderByDescending(v => v.RiskScore).ThenBy(v => v.DisplayName).ToListAsync();
    }

    public async Task<IdentityScore?> IdentityScoreAsync(int analysisId, int identityId)
    {
        await using var db = await NewDbAsync();
        return await db.IdentityScores.AsNoTracking().Include(s => s.Identity).Include(s => s.Analysis)
            .FirstOrDefaultAsync(s => s.AnalysisId == analysisId && s.IdentityId == identityId);
    }

    public async Task<List<PermissionUsage>> UsageAsync(int analysisId, int identityId)
    {
        await using var db = await NewDbAsync();
        return await db.PermissionUsages.AsNoTracking().Where(p => p.AnalysisId == analysisId && p.IdentityId == identityId)
            .OrderByDescending(p => p.EventCount).ThenBy(p => p.Action).ToListAsync();
    }

    /* ------------------------------------------------------------------ findings */

    public async Task<List<Finding>> FindingsAsync(int analysisId, string? category = null, int? identityId = null, string? severity = null)
    {
        await using var db = await NewDbAsync();
        var q = db.Findings.AsNoTracking().Include(f => f.Identity).Where(f => f.AnalysisId == analysisId);
        if (!string.IsNullOrEmpty(category)) q = q.Where(f => f.Category == category);
        if (identityId is not null) q = q.Where(f => f.IdentityId == identityId);
        if (!string.IsNullOrEmpty(severity)) q = q.Where(f => f.Severity == severity);
        return await q.OrderBy(f => f.FindingId).ToListAsync();
    }

    /// <summary>findings.js Export: CSV with the prototype columns.</summary>
    public static string FindingsCsv(IEnumerable<Finding> rows)
    {
        var sb = new StringBuilder("Identity,Action,Category,Last Used,Role,Severity\n");
        foreach (var f in rows)
            sb.Append(string.Join(",", new[] { f.Identity.UserName, f.Action, f.Category, f.LastUsed is null ? "Never" : Fmt.Date(f.LastUsed.Value), f.GrantedByRole, f.Severity }.Select(Csv))).Append('\n');
        return sb.ToString();
    }

    /// <summary>identities.js Export: CSV with the prototype columns.</summary>
    public static string IdentitiesCsv(IEnumerable<VwIdentityRisk> rows)
    {
        var sb = new StringBuilder("Name,User Name,Type,Role,Scope,Granted,Used,Score,Risk\n");
        foreach (var r in rows)
            sb.Append(string.Join(",", new[] { r.DisplayName, r.UserName, r.IdentityType, r.AssignedRole, r.ScopeName ?? r.Scope, r.GrantedCount.ToString(), r.UsedCount.ToString(), r.RiskScore.ToString(), r.RiskLevel ?? "" }.Select(Csv))).Append('\n');
        return sb.ToString();
    }

    private static string Csv(string? v) => v is null ? "" : v.Contains(',') || v.Contains('"') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

    /* ------------------------------------------------------------------ generated roles */

    public async Task<List<GeneratedRole>> RolesAsync(int analysisId)
    {
        await using var db = await NewDbAsync();
        return await db.GeneratedRoles.AsNoTracking().Include(r => r.Identity).Include(r => r.GeneratedRoleActions).Include(r => r.ReviewedByNavigation)
            .Where(r => r.AnalysisId == analysisId).OrderBy(r => r.GeneratedRoleId).ToListAsync();
    }

    public async Task<GeneratedRole?> RoleAsync(int roleId)
    {
        await using var db = await NewDbAsync();
        return await db.GeneratedRoles.AsNoTracking().Include(r => r.Identity).Include(r => r.GeneratedRoleActions).Include(r => r.ReviewedByNavigation)
            .FirstOrDefaultAsync(r => r.GeneratedRoleId == roleId);
    }

    /// <summary>roles.js "Mark as Reviewed" / "Mark as Pending".</summary>
    public async Task SetReviewedAsync(int roleId, bool reviewed, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var role = await db.GeneratedRoles.FirstOrDefaultAsync(r => r.GeneratedRoleId == roleId) ?? throw new UserFacingException("Role not found.");
        role.IsReviewed = reviewed;
        role.ReviewedBy = reviewed ? actorId : null;
        role.ReviewedAt = reviewed ? UtcNow : null;
        Audit.Add(db, actorId, reviewed ? "Role Reviewed" : "Role Moved to Pending", role.RoleName, reviewed ? "Marked as reviewed" : "Moved back to pending", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>roles.js "Include baseline read actions" checkbox (stored per role).</summary>
    public async Task SetBaselineReadAsync(int roleId, bool include, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var role = await db.GeneratedRoles.FirstOrDefaultAsync(r => r.GeneratedRoleId == roleId) ?? throw new UserFacingException("Role not found.");
        role.IncludeBaselineRead = include;
        Audit.Add(db, actorId, "Role Updated", role.RoleName, include ? "Baseline read actions included" : "Baseline read actions excluded", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>
    /// roles.js actionsFor(): the used (non-rare) actions; "/read" actions only when baseline read is on.
    /// Rare actions are listed separately for review (they are not in the JSON until confirmed).
    /// </summary>
    public static List<string> RoleActions(GeneratedRole role)
    {
        var used = role.GeneratedRoleActions.Where(a => !a.IsRare).Select(a => a.Action).ToList();
        var write = used.Where(a => !a.EndsWith("/read", StringComparison.OrdinalIgnoreCase));
        var read = used.Where(a => a.EndsWith("/read", StringComparison.OrdinalIgnoreCase));
        return role.IncludeBaselineRead ? write.Concat(read).ToList() : write.ToList();
    }

    /// <summary>roles.js roleJson(): the Azure custom role definition.</summary>
    public static string RoleJson(GeneratedRole role) =>
        JsonSerializer.Serialize(new
        {
            Name = role.RoleName,
            IsCustom = true,
            Description = "Least-privilege role generated by SecureShift Cloud Governance for " + role.Identity.UserName,
            Actions = RoleActions(role),
            NotActions = Array.Empty<string>(),
            AssignableScopes = new[] { "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/" + role.AssignableScope },
        }, new JsonSerializerOptions { WriteIndented = true });

    /* ------------------------------------------------------------------ rules + patterns */

    public async Task<RuleSetting> ActiveRulesAsync()
    {
        await using var db = await NewDbAsync();
        return await db.RuleSettings.AsNoTracking().Include(r => r.UpdatedByNavigation).Where(r => r.IsActive)
            .OrderByDescending(r => r.RuleSettingId).FirstOrDefaultAsync() ?? AnalysisService.DefaultRules();
    }

    /// <summary>rules.js Save: weights must add up to 100, rare threshold 1-50. Saved as a new active row (history kept).</summary>
    public async Task SaveRulesAsync(int wUnused, int wHighRisk, int wScope, int periodDays, int rare, bool baselineRead, int actorId, string action = "Rules Updated")
    {
        if (wUnused + wHighRisk + wScope != 100) throw new UserFacingException("Weights must add up to 100% before saving");
        if (rare is < 1 or > 50) throw new UserFacingException("Rare action threshold must be 1 to 50");
        if (periodDays is < 7 or > 180) throw new UserFacingException("Default analysis window must be 7 to 180 days");

        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var old in await db.RuleSettings.Where(r => r.IsActive).ToListAsync()) old.IsActive = false;
        db.RuleSettings.Add(new RuleSetting
        {
            WeightUnused = wUnused, WeightHighRisk = wHighRisk, WeightScope = wScope, DefaultPeriodDays = periodDays,
            RareThreshold = rare, IncludeBaselineRead = baselineRead, IsActive = true, UpdatedBy = actorId, UpdatedAt = UtcNow,
        });
        Audit.Add(db, actorId, action, "rules.json", $"Weights {wUnused}/{wHighRisk}/{wScope}, window {periodDays} days, rare < {rare}, baseline read {(baselineRead ? "on" : "off")}", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>rules.js "Reset to default": weights, settings AND the 4 default patterns.</summary>
    public async Task ResetRulesAsync(int actorId)
    {
        await SaveRulesAsync(50, 30, 20, 90, 3, true, actorId, "Rules Reset");
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var defaults = new[] { "*/delete", "Microsoft.Authorization/*/write", "Microsoft.KeyVault/*", "Microsoft.Network/networkSecurityGroups/write" };
        var all = await db.HighRiskPatterns.ToListAsync();
        db.HighRiskPatterns.RemoveRange(all.Where(p => !defaults.Contains(p.Pattern)));
        foreach (var p in all.Where(p => defaults.Contains(p.Pattern))) p.IsEnabled = true;
        foreach (var d in defaults.Where(d => all.All(p => p.Pattern != d))) db.HighRiskPatterns.Add(new HighRiskPattern { Pattern = d, IsEnabled = true });
        Audit.Add(db, actorId, "Patterns Reset", "rules.json", "Default high-risk patterns restored", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>The "rules.json (current values)" view of the prototype.</summary>
    public static string RulesJson(RuleSetting r, IEnumerable<HighRiskPattern> patterns) =>
        JsonSerializer.Serialize(new
        {
            weights = new { unused = r.WeightUnused, highRisk = r.WeightHighRisk, scope = r.WeightScope },
            patterns = patterns.Select(p => new { p = p.Pattern, on = p.IsEnabled }),
            window = r.DefaultPeriodDays + " days",
            baselineRead = r.IncludeBaselineRead,
            rareThreshold = r.RareThreshold,
        }, new JsonSerializerOptions { WriteIndented = true });

    public async Task<List<HighRiskPattern>> PatternsAsync()
    {
        await using var db = await NewDbAsync();
        return await db.HighRiskPatterns.AsNoTracking().OrderBy(p => p.PatternId).ToListAsync();
    }

    /// <summary>rules.js "Add High-Risk Pattern" (the DB allows letters, numbers and . / * - only).</summary>
    public static string CheckPattern(string v, IEnumerable<HighRiskPattern> existing)
    {
        if (v.Length == 0) return "Pattern is required";
        if (v.Length > 200) return "Max 200 characters";
        if (!PatternRx().IsMatch(v)) return "Only letters, numbers, . / * - are allowed";
        return existing.Any(p => p.Pattern.Equals(v, StringComparison.OrdinalIgnoreCase)) ? "This pattern already exists" : "";
    }

    public async Task AddPatternAsync(string pattern, int actorId)
    {
        pattern = pattern.Trim();
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var error = CheckPattern(pattern, await db.HighRiskPatterns.AsNoTracking().ToListAsync());
        if (error.Length > 0) throw new UserFacingException("Pattern: " + error);
        db.HighRiskPatterns.Add(new HighRiskPattern { Pattern = pattern, IsEnabled = true });
        Audit.Add(db, actorId, "Pattern Added", pattern, "High-risk pattern", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task SetPatternEnabledAsync(int patternId, bool enabled, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = await db.HighRiskPatterns.FirstOrDefaultAsync(x => x.PatternId == patternId) ?? throw new UserFacingException("Pattern not found.");
        p.IsEnabled = enabled;
        Audit.Add(db, actorId, enabled ? "Pattern Enabled" : "Pattern Disabled", p.Pattern, "High-risk pattern", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DeletePatternAsync(int patternId, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = await db.HighRiskPatterns.FirstOrDefaultAsync(x => x.PatternId == patternId) ?? throw new UserFacingException("Pattern not found.");
        db.HighRiskPatterns.Remove(p);
        Audit.Add(db, actorId, "Pattern Removed", p.Pattern, "High-risk pattern", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /* ------------------------------------------------------------------ reports */

    public async Task<List<Report>> ReportsAsync()
    {
        await using var db = await NewDbAsync();
        return await db.Reports.AsNoTracking().Include(r => r.Analysis).ThenInclude(a => a.Company).Include(r => r.GeneratedByNavigation)
            .OrderByDescending(r => r.GeneratedAt).ThenByDescending(r => r.ReportId).ToListAsync();
    }

    /// <summary>reports.js "Generate New Report": uses the latest analysis of the company; RPT-(highest number + 1).</summary>
    public async Task<Report> GenerateReportAsync(int companyId, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var company = await db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId) ?? throw new UserFacingException("Company not found.");
        var latest = await db.Analyses.Where(a => a.CompanyId == companyId).OrderByDescending(a => a.RunAt).ThenByDescending(a => a.AnalysisId).FirstOrDefaultAsync()
                     ?? throw new UserFacingException($"{company.CompanyName} has no analysis yet. Run the Analysis Wizard first.");
        var numbers = (await db.Reports.Select(r => r.Code).ToListAsync())
            .Select(c => int.TryParse(Regex.Replace(c, "[^0-9]", ""), out var n) ? n : 0).DefaultIfEmpty(1110);
        var report = new Report
        {
            Code = "RPT-" + (numbers.Max() + 1), AnalysisId = latest.AnalysisId, IdentityCount = latest.IdentityCount ?? 0,
            AvgScore = latest.AvgScore ?? 0, HighRiskCount = latest.HighRiskCount ?? 0, GeneratedBy = actorId, GeneratedAt = UtcNow,
        };
        db.Reports.Add(report);
        Audit.Add(db, actorId, "Report Generated", report.Code, $"{company.CompanyName} · {latest.Code}", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return report;
    }

    /// <summary>"Fix First" (top 3) and the risk bands of the report's analysis, from the real scores.</summary>
    public async Task<(List<VwIdentityRisk> FixFirst, int High, int Medium, int Low)> ReportDetailAsync(int analysisId)
    {
        await using var db = await NewDbAsync();
        var scores = await db.VwIdentityRisks.AsNoTracking().Where(v => v.AnalysisId == analysisId).OrderByDescending(v => v.RiskScore).ThenBy(v => v.DisplayName).ToListAsync();
        return (scores.Take(3).ToList(), scores.Count(s => s.RiskScore >= 70), scores.Count(s => s.RiskScore is >= 40 and < 70), scores.Count(s => s.RiskScore < 40));
    }

    /// <summary>reports.js fullReport(): a printable HTML file.</summary>
    public async Task<string> ReportHtmlAsync(Report report)
    {
        await using var db = await NewDbAsync();
        var ids = await db.VwIdentityRisks.AsNoTracking().Where(v => v.AnalysisId == report.AnalysisId).OrderByDescending(v => v.RiskScore).ToListAsync();
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='UTF-8'><title>").Append(E(report.Code)).Append("</title><style>body{font-family:Arial,sans-serif;max-width:860px;margin:30px auto;color:#111}")
          .Append("table{border-collapse:collapse;width:100%;margin-top:10px}th,td{border:1px solid #ccc;padding:8px;text-align:left;font-size:13px}th{background:#f5f5f5}")
          .Append(".k{display:inline-block;border:1px solid #ccc;border-radius:6px;padding:10px 16px;margin:4px;text-align:center}.k b{display:block;font-size:22px}</style></head><body>")
          .Append("<h1>Cloud Governance Report</h1><p>").Append(E(report.Code)).Append(" · ").Append(E(report.Analysis.Company.CompanyName)).Append(" · Generated ").Append(Fmt.Date(DateOnly.FromDateTime(report.GeneratedAt))).Append("</p>")
          .Append("<div><span class='k'><b>").Append(report.IdentityCount).Append("</b>Identities</span><span class='k'><b>").Append(Fmt.Dec2(report.AvgScore)).Append("</b>Average score</span><span class='k'><b>")
          .Append(report.HighRiskCount).Append("</b>High-risk unused</span></div><h2>Identities</h2><table><tr><th>Identity</th><th>Type</th><th>Role</th><th>Scope</th><th>Granted</th><th>Used</th><th>Score</th></tr>");
        foreach (var i in ids)
            sb.Append("<tr><td>").Append(E(i.DisplayName)).Append(" (").Append(E(i.UserName)).Append(")</td><td>").Append(E(i.IdentityType)).Append("</td><td>").Append(E(i.AssignedRole))
              .Append("</td><td>").Append(E(i.ScopeName ?? i.Scope)).Append("</td><td>").Append(i.GrantedCount).Append("</td><td>").Append(i.UsedCount).Append("</td><td>").Append(i.RiskScore).Append("</td></tr>");
        sb.Append("</table><p style='color:#777;font-size:12px;margin-top:20px'>© 2026 SecureShift · Offline analysis, no data leaves this machine.</p></body></html>");
        return sb.ToString();
    }

    /* ------------------------------------------------------------------ notifications */

    public async Task<List<Notification>> NotificationsAsync(int userId)
    {
        await using var db = await NewDbAsync();
        return await db.Notifications.AsNoTracking().Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId).ToListAsync();
    }

    public async Task<int> UnreadCountAsync(int userId)
    {
        await using var db = await NewDbAsync();
        return await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    /// <summary>"Mark all read" (shell.js). Returns how many were unread.</summary>
    public async Task<int> MarkAllReadAsync(int userId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return unread.Count;
    }

    /* ------------------------------------------------------------------ files */

    /// <summary>Saves an export (CSV / JSON / HTML) and returns its full path. Default folder: "exports" in the current folder.</summary>
    public static async Task<string> SaveExportAsync(string? folder, string fileName, string content)
    {
        var dir = Path.GetFullPath(string.IsNullOrWhiteSpace(folder) ? "exports" : folder.Trim().Trim('"'));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
        return path;
    }
}
