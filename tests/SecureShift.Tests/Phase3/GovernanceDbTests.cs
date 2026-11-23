using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Governance;

namespace SecureShift.Tests.Phase3;

/// <summary>The analysis run (one transaction) and the Cloud Admin services against a fresh database.</summary>
public class GovernanceDbTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private const int Aarav = 25;   // Cloud Admin
    private static readonly FixedClock Clock = new(new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero));

    private T Get<T>() where T : notnull => db.BuildServices(clock: Clock).GetRequiredService<T>();

    private static Task<GovernanceInput> SampleInput() =>
        GovernanceInputReader.LoadAsync(Samples.Assignments, Samples.Activity, Samples.Catalog, Samples.From, Samples.To, "Northwind Traders");

    [Fact]
    public async Task Analysis_saves_scores_usage_findings_roles_notifications_and_audit_in_one_go()
    {
        var input = await SampleInput();
        await using var before = db.CreateContext();
        var identitiesBefore = await before.Identities.CountAsync(i => i.CompanyId == 1);
        var newNames = input.Assignments.Select(a => a.UserName).Except(await before.Identities.Where(i => i.CompanyId == 1).Select(i => i.UserName).ToListAsync()).Count();

        var analysis = await Get<AnalysisService>().RunAsync(1, input, Aarav);

        await using var ctx = db.CreateContext();
        var saved = await ctx.Analyses.AsNoTracking().SingleAsync(a => a.AnalysisId == analysis.AnalysisId);
        Assert.Matches("^AN-2026-11[0-9]{2}$", saved.Code);
        Assert.Equal(("Complete", 90, 20), (saved.Status, saved.PeriodDays, saved.IdentityCount));
        Assert.Equal(identitiesBefore + newNames, await ctx.Identities.CountAsync(i => i.CompanyId == 1));   // existing identities reused
        Assert.Equal(1, await ctx.Identities.CountAsync(i => i.CompanyId == 1 && i.UserName == "svc-webapp-deploy"));

        var scores = await ctx.IdentityScores.AsNoTracking().Include(s => s.Identity).Where(s => s.AnalysisId == saved.AnalysisId).ToListAsync();
        Assert.Equal(20, scores.Count);
        Assert.All(scores, s => Assert.Equal(GovernanceScoring.Score(s.GrantedCount, s.UsedCount, s.UnusedHighRisk, s.Identity.Scope, 50, 30, 20), s.RiskScore));
        Assert.All(scores, s => Assert.Equal(GovernanceScoring.RiskOf(s.RiskScore), s.RiskLevel));
        var webapp = scores.Single(s => s.Identity.UserName == "svc-webapp-deploy");
        Assert.Equal((76, 14, 91, "rg-webapp-prod"), (webapp.GrantedCount, webapp.UsedCount, webapp.RiskScore, webapp.SuggestedScope));
        Assert.Equal((decimal)JsMath.Round2(scores.Average(s => s.RiskScore)), saved.AvgScore);

        var usage = await ctx.PermissionUsages.AsNoTracking().Where(p => p.AnalysisId == saved.AnalysisId && p.IdentityId == webapp.IdentityId).ToListAsync();
        Assert.Equal(212, usage.Single(u => u.Action == "Microsoft.Web/sites/read").EventCount);   // same as the prototype's SS.GOV.detail
        Assert.Equal(new DateOnly(2026, 8, 14), usage.Single(u => u.Action == "Microsoft.Web/sites/write").FirstUsed);

        var findings = await ctx.Findings.AsNoTracking().Where(f => f.AnalysisId == saved.AnalysisId).ToListAsync();
        Assert.Equal(saved.HighRiskCount, findings.Count);
        Assert.All(findings, f => Assert.Contains(f.Category, GovernanceService.Categories));
        Assert.All(findings, f => Assert.Null(f.LastUsed));

        var role = await ctx.GeneratedRoles.AsNoTracking().Include(r => r.GeneratedRoleActions).SingleAsync(r => r.AnalysisId == saved.AnalysisId && r.IdentityId == webapp.IdentityId);
        Assert.Equal(("svc-webapp-deploy-custom-role", "Contributor (Subscription)", "rg-webapp-prod"), (role.RoleName, role.OriginalRole, role.AssignableScope));
        Assert.Equal(["Microsoft.Web/sites/backup/action - used once, 70 days ago", "Microsoft.Web/sites/delete - used once, 85 days ago"],
            role.GeneratedRoleActions.Where(a => a.IsRare).OrderBy(a => a.Action).Select(a => $"{a.Action} - {a.Note}"));   // roles.js texts
        role.Identity = webapp.Identity;
        Assert.Equal(12, GovernanceService.RoleActions(role).Count);   // the 12 used permissions of the prototype

        var notes = await ctx.Notifications.AsNoTracking().Where(n => n.UserId == Aarav).OrderByDescending(n => n.NotificationId).Take(3).Select(n => n.Message).ToListAsync();
        Assert.Contains($"Analysis {saved.Code} completed for Northwind Traders", notes);
        Assert.Contains(notes, n => n.EndsWith("generated roles are waiting for review"));
        Assert.Contains($"{findings.Count} new high-risk unused permissions found", notes);
        Assert.True(await ctx.AuditLogs.AnyAsync(a => a.Action == "Analysis Run" && a.Target == saved.Code && a.UserId == Aarav));
    }

    [Fact]
    public async Task A_failing_analysis_saves_nothing()
    {
        var input = await SampleInput();
        var tooLong = "Microsoft.Web/" + new string('x', 250) + "/read";   // longer than PermissionUsage.Action (200)
        var bad = input with
        {
            Roles = input.Roles.Select(r => r with { Actions = [.. r.Actions, tooLong] }).ToList(),
            Events = [.. input.Events, new ActivityEventRow("rahul.verma@northwindtraders.com", tooLong, new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero), "rg-finance", "Succeeded")],
        };
        await using var ctx = db.CreateContext();
        var counts = async () => (await ctx.Analyses.CountAsync(), await ctx.IdentityScores.CountAsync(), await ctx.Findings.CountAsync(), await ctx.Notifications.CountAsync(), await ctx.AuditLogs.CountAsync());
        var before = await counts();
        await Assert.ThrowsAsync<DbUpdateException>(() => Get<AnalysisService>().RunAsync(1, bad, Aarav));
        Assert.Equal(before, await counts());
    }

    [Fact]
    public async Task Rule_weights_are_used_by_the_next_analysis()
    {
        var gov = Get<GovernanceService>();
        Assert.Equal("Weights must add up to 100% before saving", (await Assert.ThrowsAsync<UserFacingException>(() => gov.SaveRulesAsync(50, 30, 30, 90, 3, true, Aarav))).Message);
        Assert.Equal("Rare action threshold must be 1 to 50", (await Assert.ThrowsAsync<UserFacingException>(() => gov.SaveRulesAsync(50, 30, 20, 90, 0, true, Aarav))).Message);

        await gov.SaveRulesAsync(100, 0, 0, 60, 3, false, Aarav);
        var rules = await gov.ActiveRulesAsync();
        Assert.Equal((100, 0, 0, 60, false), (rules.WeightUnused, rules.WeightHighRisk, rules.WeightScope, rules.DefaultPeriodDays, rules.IncludeBaselineRead));
        await using (var ctx = db.CreateContext()) Assert.Equal(1, await ctx.RuleSettings.CountAsync(r => r.IsActive));

        var analysis = await Get<AnalysisService>().RunAsync(2, await SampleInput(), Aarav);   // Contoso Retail
        await using (var ctx = db.CreateContext())
        {
            var scores = await ctx.IdentityScores.AsNoTracking().Where(s => s.AnalysisId == analysis.AnalysisId).ToListAsync();
            Assert.All(scores, s => Assert.Equal(GovernanceScoring.UnusedRatio(s.GrantedCount, s.UsedCount), s.RiskScore));   // 100% unused ratio
            Assert.All(await ctx.GeneratedRoles.Where(r => r.AnalysisId == analysis.AnalysisId).ToListAsync(), r => Assert.False(r.IncludeBaselineRead));
        }
        await gov.ResetRulesAsync(Aarav);
        Assert.Equal(50, (await gov.ActiveRulesAsync()).WeightUnused);
    }

    [Fact]
    public async Task Patterns_add_toggle_delete_and_validation()
    {
        var gov = Get<GovernanceService>();
        var list = await gov.PatternsAsync();
        Assert.Equal("Pattern is required", GovernanceService.CheckPattern("", list));
        Assert.Equal("Only letters, numbers, . / * - are allowed", GovernanceService.CheckPattern("Microsoft.Sql/*/del ete", list));
        Assert.Equal("Only letters, numbers, . / * - are allowed", GovernanceService.CheckPattern("my_pattern", list));   // the DB CHECK has no underscore
        Assert.Equal("This pattern already exists", GovernanceService.CheckPattern("*/DELETE", list));

        await gov.AddPatternAsync("Microsoft.Sql/*/write", Aarav);
        var added = (await gov.PatternsAsync()).Single(p => p.Pattern == "Microsoft.Sql/*/write");
        Assert.True(added.IsEnabled);
        await gov.SetPatternEnabledAsync(added.PatternId, false, Aarav);
        Assert.False((await gov.PatternsAsync()).Single(p => p.PatternId == added.PatternId).IsEnabled);
        await gov.DeletePatternAsync(added.PatternId, Aarav);
        Assert.DoesNotContain(await gov.PatternsAsync(), p => p.PatternId == added.PatternId);
    }

    [Fact]
    public async Task Companies_reports_roles_and_notifications()
    {
        var gov = Get<GovernanceService>();
        var ex = await Assert.ThrowsAsync<UserFacingException>(() => gov.AddCompanyAsync("NW", "short", "0", Aarav));
        Assert.Contains("Enter at least 3 characters", ex.Message);
        Assert.Contains("Enter a valid tenant ID", ex.Message);
        Assert.Contains("At least 1", ex.Message);
        Assert.Contains("This company already exists", (await Assert.ThrowsAsync<UserFacingException>(() => gov.AddCompanyAsync("northwind traders", "12345678-aaaa", "1", Aarav))).Message);
        var blue = await gov.AddCompanyAsync("Blue Yonder Retail", "0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0", "2", Aarav);
        Assert.Contains("has no analysis yet", (await Assert.ThrowsAsync<UserFacingException>(() => gov.GenerateReportAsync(blue.CompanyId, Aarav))).Message);

        var report = await gov.GenerateReportAsync(3, Aarav);   // Fabrikam Logistics: latest analysis AN-2026-1020
        Assert.Matches("^RPT-11[0-9]{2}$", report.Code);
        Assert.Equal((91, 62m, 30), (report.IdentityCount, report.AvgScore, report.HighRiskCount));

        // seeded role 1 (svc-webapp-deploy): 12 actions with baseline read, 5 write actions without
        var role = (await gov.RoleAsync(1))!;
        Assert.Equal(12, GovernanceService.RoleActions(role).Count);
        await gov.SetBaselineReadAsync(1, false, Aarav);
        Assert.Equal(5, GovernanceService.RoleActions((await gov.RoleAsync(1))!).Count);
        Assert.Contains("\"AssignableScopes\"", GovernanceService.RoleJson(role));
        await gov.SetReviewedAsync(1, true, Aarav);
        var reviewed = (await gov.RoleAsync(1))!;
        Assert.True(reviewed.IsReviewed);
        Assert.Equal(Aarav, reviewed.ReviewedBy);

        var unread = await gov.UnreadCountAsync(Aarav);
        Assert.True(unread >= 1);
        Assert.Equal(unread, await gov.MarkAllReadAsync(Aarav));
        Assert.Equal(0, await gov.UnreadCountAsync(Aarav));
    }

    [Fact]
    public async Task Seeded_findings_and_identities_match_the_prototype_pages()
    {
        var gov = Get<GovernanceService>();
        var findings = await gov.FindingsAsync(1);
        Assert.Equal(37, findings.Count);
        Assert.Equal([14, 8, 7, 8], GovernanceService.Categories.Select(c => findings.Count(f => f.Category == c)));   // governance-data.js target counts
        var high = await gov.IdentitiesAsync(1, null, null, "HIGH");
        Assert.All(high, h => Assert.True(h.RiskScore >= 70));
        Assert.Equal("Web App Deploy Pipeline", (await gov.IdentitiesAsync(1, "deploy", "Service Principal", null))[0].DisplayName);
    }
}
