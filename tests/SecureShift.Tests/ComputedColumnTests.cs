using Microsoft.EntityFrameworkCore;

namespace SecureShift.Tests;

/// <summary>
/// The GENERATED ALWAYS AS ... STORED columns give the same values as the SQL Server computed
/// columns and the prototype JS (codes like APP-001, SS.GOV.riskOf, SS.EMDT.level, categoryOf).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ComputedColumnTests(DatabaseFixture fixture)
{
    // governance-data.js: SS.GOV.riskOf
    private static string GovRiskOf(int score) => score >= 70 ? "HIGH" : score >= 40 ? "MEDIUM" : "LOW";
    // emdt-calc.js: C.level
    private static string EmdtLevel(int score) => score >= 70 ? "High" : score >= 40 ? "Medium" : "Low";
    // emdt-calc.js: C.categoryOf (min 0 / 25 / 45 / 70)
    private static string CategoryOf(decimal emsi) => emsi >= 70 ? "Strategic" : emsi >= 45 ? "Enterprise" : emsi >= 25 ? "Growth" : "Foundation";

    [Fact]
    public async Task Code_columns_are_built_from_the_id()
    {
        await using var db = fixture.CreateContext();

        Assert.Equal("USR-001", (await db.Users.AsNoTracking().SingleAsync(x => x.UserId == 1)).Code);
        Assert.Equal("USR-025", (await db.Users.AsNoTracking().SingleAsync(x => x.UserId == 25)).Code);
        Assert.Equal("ENT-002", (await db.Enterprises.AsNoTracking().SingleAsync(x => x.EnterpriseId == 2)).Code);
        Assert.Equal("APP-010", (await db.Applications.AsNoTracking().SingleAsync(x => x.ApplicationId == 10)).Code);
        Assert.Equal("N-01", (await db.Components.AsNoTracking().SingleAsync(x => x.ComponentId == 1)).Code);
        Assert.Equal("N-19", (await db.Components.AsNoTracking().SingleAsync(x => x.ComponentId == 19)).Code);
        Assert.Equal("DEP-020", (await db.Dependencies.AsNoTracking().SingleAsync(x => x.DependencyId == 20)).Code);
        Assert.Equal("ASM-005", (await db.Assessments.AsNoTracking().SingleAsync(x => x.AssessmentId == 5)).Code);
        Assert.Equal("SIM-006", (await db.Simulations.AsNoTracking().SingleAsync(x => x.SimulationId == 6)).Code);
        Assert.Equal("F-37", (await db.Findings.AsNoTracking().SingleAsync(x => x.FindingId == 37)).Code);

        var plan4 = await db.Plans.AsNoTracking().SingleAsync(x => x.PlanName == "Online Store Replatform Plan");
        Assert.Equal("PLAN-004", plan4.Code);
        Assert.Equal("Approved", plan4.Status);
    }

    [Theory]
    [InlineData("AN-2026-1110", 90)]
    [InlineData("AN-2026-1103", 90)]
    [InlineData("AN-2026-1027", 30)]
    [InlineData("AN-2026-1020", 90)]
    public async Task Analysis_period_days_count_both_ends(string code, int days)
    {
        await using var db = fixture.CreateContext();
        var analysis = await db.Analyses.AsNoTracking().SingleAsync(a => a.Code == code);
        Assert.Equal(days, analysis.PeriodDays);
        Assert.Equal(analysis.PeriodTo.DayNumber - analysis.PeriodFrom.DayNumber + 1, analysis.PeriodDays);
    }

    [Fact]
    public async Task Identity_risk_level_matches_SS_GOV_riskOf()
    {
        await using var db = fixture.CreateContext();
        var scores = await db.IdentityScores.AsNoTracking().ToListAsync();

        Assert.Equal(128, scores.Count);
        Assert.All(scores, s => Assert.Equal(GovRiskOf(s.RiskScore), s.RiskLevel));
        // boundary rows from the seed: 91 / 71 -> HIGH, 40 -> MEDIUM, 39 -> LOW
        Assert.Equal("HIGH", scores.Single(s => s.IdentityId == 1).RiskLevel);
        Assert.Equal("HIGH", scores.Single(s => s.IdentityId == 3).RiskLevel);
        Assert.Equal("MEDIUM", scores.Single(s => s.IdentityId == 94).RiskLevel);
        Assert.Equal("LOW", scores.Single(s => s.IdentityId == 12).RiskLevel);
    }

    [Fact]
    public async Task Simulation_risk_level_and_EMSI_category_match_emdt_calc()
    {
        await using var db = fixture.CreateContext();

        var sims = await db.Simulations.AsNoTracking().ToListAsync();
        Assert.All(sims, s => Assert.Equal(EmdtLevel(s.ModernizationRisk), s.RiskLevel));

        var classifications = await db.Classifications.AsNoTracking().Include(c => c.Enterprise).ToListAsync();
        Assert.All(classifications, c => Assert.Equal(CategoryOf(c.EmsiScore), c.Category));
        Assert.Equal("Strategic", classifications.Single(c => c.Enterprise.Name == "Northwind Bank").Category);   // EMSI 77.87
        Assert.Equal("Growth", classifications.Single(c => c.Enterprise.Name == "Contoso Retail").Category);     // EMSI 44.91
    }

    [Fact]
    public async Task Latest_assessment_view_has_one_row_per_assessed_application()
    {
        await using var db = fixture.CreateContext();
        var latest = await db.VwLatestAssessments.AsNoTracking().OrderBy(v => v.ApplicationId).ToListAsync();

        Assert.Equal(new int?[] { 1, 3, 5, 6, 7 }, latest.Select(v => v.ApplicationId));
        Assert.Equal("ASM-001", latest[0].Code);
    }
}
