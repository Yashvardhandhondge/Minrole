using Microsoft.EntityFrameworkCore;

namespace SecureShift.Tests.Phase3;

/// <summary>The Cloud Admin menus driven by scripted keyboard input, against a fresh database.</summary>
public class Phase3MenuTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private static readonly string[] Login = ["1", "aarav.mehta@secureshift.io", "Cloud@2026"];
    private static readonly string[] Logout = ["0", "y", "0"];

    [Fact]
    public async Task Wizard_with_a_bad_file_then_the_good_files()
    {
        var (code, output) = await db.RunAppAsync([
            .. Login,
            "3", "1", "4", "08/12/2026", "11/09/2026", Samples.Path("bad/role-assignments-missing-fields.json"), Samples.Activity, Samples.Catalog,
            "3", "1", "4", "08/12/2026", "11/09/2026", "missing-file.json", Samples.Assignments, Samples.Path("bad/activity-log-broken.json"), Samples.Catalog,
            "3", "1", "4", "08/12/2026", "11/09/2026", Samples.Assignments, Samples.Activity, Samples.Catalog, "y",
            .. Logout]);
        Assert.Equal(0, code);
        Assert.Contains("[ERROR] Missing fields check - role-assignments-missing-fields.json record 2 has no principalId, scope", output);
        Assert.Contains("file not found", output);
        Assert.Contains("activity-log-broken.json is not valid JSON (line 3", output);
        Assert.Contains("[OK] Activity coverage - Activity Log covers 08/12/2026 - 11/09/2026 (90 days) for Northwind Traders", output);
        Assert.Contains("Analysis Wizard - Step 4 of 4: Results", output);
        Assert.Contains("Identities analysed", output);
        Assert.DoesNotContain("Exception", output);

        await using var ctx = db.CreateContext();
        Assert.Equal(5, await ctx.Analyses.CountAsync());   // 4 seeded + 1: the failed attempts saved nothing
    }

    [Fact]
    public async Task Dashboard_identity_detail_findings_and_roles()
    {
        var (_, output) = await db.RunAppAsync([
            .. Login,
            "1",                       // dashboard
            "5", "1", "0", "0",        // identities of the latest analysis -> open the first -> back
            "6", "K", "Key Vault", "0", // findings filtered by category
            "7", "1", "1", "0", "0",   // generated roles -> first -> mark as reviewed -> back
            .. Logout]);
        Assert.Contains("Welcome back, Aarav Mehta", output);
        Assert.Contains("Top 5 identities to fix", output);
        Assert.Contains("Why this score", output);
        Assert.Contains("Category: Key Vault", output);
        Assert.Contains("Role definition (JSON):", output);
        Assert.Contains("az role definition create --role-definition", output);
        Assert.Contains("marked as reviewed", output);
    }

    [Fact]
    public async Task Rules_patterns_reports_companies_and_notifications()
    {
        var (_, output) = await db.RunAppAsync([
            .. Login,
            "8", "1", "50", "30", "30", "90 days", "3", "Yes",       // weights 110% -> refused
            "1", "60", "20", "20", "60 days", "5", "No", "0",        // valid -> saved
            "9", "A", "bad pattern!", "Microsoft.Sql/*/write", "0",  // invalid then valid pattern
            "10", "G", "Fabrikam Logistics", "1", "n", "0",          // generate a report, preview it
            "2", "A", "Blue Yonder Retail", "0f1e2d3c-4b5a", "2", "0", // add a company
            "11", "y",                                               // notifications, mark all read
            .. Logout]);
        Assert.Contains("[ERROR] Weights must add up to 100% before saving", output);
        Assert.Contains("[OK] rules.json saved", output);
        Assert.Contains("[ERROR] Only letters, numbers, . / * - are allowed", output);
        Assert.Contains("[OK] Pattern added", output);
        Assert.Contains("generated for Fabrikam Logistics", output);
        Assert.Contains("Fix First:", output);
        Assert.Contains("Blue Yonder Retail added. Run the Analysis Wizard next.", output);
        Assert.Contains("All notifications marked as read", output);

        await using var ctx = db.CreateContext();
        var rules = await ctx.RuleSettings.AsNoTracking().SingleAsync(r => r.IsActive);
        Assert.Equal((60, 20, 20, 60, 5, false), (rules.WeightUnused, rules.WeightHighRisk, rules.WeightScope, rules.DefaultPeriodDays, rules.RareThreshold, rules.IncludeBaselineRead));
        Assert.True(await ctx.HighRiskPatterns.AnyAsync(p => p.Pattern == "Microsoft.Sql/*/write"));
        Assert.True(await ctx.AuditLogs.AnyAsync(a => a.Action == "Report Generated"));
        Assert.True(await ctx.Companies.AnyAsync(c => c.CompanyName == "Blue Yonder Retail"));
    }
}
