using Microsoft.EntityFrameworkCore;
using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Modernization;

namespace SecureShift.Tests.Phase5;

/// <summary>Assessments, simulations, plans and decisions on a fresh database, compared with the prototype JavaScript.</summary>
public class PlanningDbTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private const int Amanjeet = 13, Isha = 21, Shivanand = 14;   // Architect, Architect, Executive
    private PlanningService Planning => db.Get<PlanningService>();

    [Fact]
    public async Task Assessment_saves_the_prototype_scores_and_factors()
    {
        var a = await Planning.RunAssessmentAsync(2, Amanjeet);   // Customer360 (not assessed in the seed)
        var p = PrototypeData.Emdt.Items("seedApps").Single(x => x.Str("id") == "APP-002");
        Assert.Equal((p.Int("td"), p.Int("gr"), p.Int("di")), (a.TechnicalDebt, a.GovernanceRisk, a.DependencyImpact));

        await using var ctx = db.CreateContext();
        var factors = await ctx.AssessmentFactors.AsNoTracking().Where(f => f.AssessmentId == a.AssessmentId).OrderBy(f => f.AssessmentFactorId).ToListAsync();
        Assert.Equal(p.Items("tdFactors").Concat(p.Items("grFactors")).Select(f => (f[0].GetString(), f[1].GetString(), f[2].GetInt32())),
            factors.Select(f => ((string?)f.FactorName, (string?)f.FactorValue, f.Points)));
        Assert.Equal("Technical Debt 47, Governance Risk 72", await ctx.AuditLogs.Where(x => x.Action == "Assessment Run" && x.Target == "Customer360").Select(x => x.Detail).FirstAsync());
        Assert.Equal(a.AssessmentId, (await ctx.VwLatestAssessments.AsNoTracking().SingleAsync(v => v.ApplicationId == 2)).AssessmentId);
    }

    [Fact]
    public async Task Every_app_and_strategy_saved_through_the_DB_gives_the_prototype_simulation()
    {
        var planning = Planning;
        await using var ctx = db.CreateContext();
        var apps = await ctx.Applications.AsNoTracking().ToDictionaryAsync(a => a.Code!, a => a.ApplicationId);
        foreach (var code in apps.Keys)
            if (!await ctx.Assessments.AnyAsync(x => x.ApplicationId == apps[code])) await planning.RunAssessmentAsync(apps[code], Amanjeet);

        foreach (var s in PrototypeData.Emdt.Items("simulations"))
        {
            var sim = await planning.RunSimulationAsync(apps[s.Str("app")], s.Str("strategy"), Amanjeet);
            var label = $"{s.Str("app")} {s.Str("strategy")}";
            Assert.True((s.Int("mr"), s.Int("effort"), s.Int("team"), (decimal)s.Num("cost"), s.Int("sr")) == (sim.ModernizationRisk, sim.EffortWeeks, sim.TeamSize, sim.CostLakh, sim.StrategyRisk), label);
            Assert.Equal(s.Str("mrLevel"), sim.RiskLevel);
            Assert.Equal(s.Str("recommendation"), sim.Recommendation);
            Assert.Equal(s.Num("emsi"), (double)(sim.EmsiScore ?? 0));
            Assert.Equal(s.Items("sequence").Select(x => x.GetString()), sim.SimulationImpacts.OrderBy(x => x.SequenceNo).Select(x => (string?)x.AffectedName));
            var b = PlanningService.Breakdown(sim);
            Assert.Equal((s.Num("baseWeeks"), s.Num("personMonths"), s.Num("labour"), s.Num("testing")), (b.BaseWeeks, b.PersonMonths, b.Labour, b.Testing));
        }
    }

    [Fact]
    public async Task Simulation_needs_an_assessment_and_can_be_discarded_until_it_has_a_plan()
    {
        var planning = Planning;
        var fresh = await db.Get<ModernizationService>().SaveApplicationAsync(new SecureShift.Data.Models.Generated.Application
        {
            EnterpriseId = 2, Name = "Gift Cards", BusinessUnit = "Retail", OwnerTeam = "Web Team", Status = "Active", Deployment = "Public Cloud", Technology = "PHP",
            Framework = "PHP 8.1", SupportStatus = "Supported", AgeYears = 2, DocQuality = "Good", Complexity = "Low", LinesOfCode = 50000, Criticality = "Low",
            HasSensitiveData = false, SecurityIssues = 0, DowntimeTolerance = "High", CompliancePolicies = 0,
        }, 12);
        Assert.Contains("not assessed yet", (await Assert.ThrowsAsync<UserFacingException>(() => planning.RunSimulationAsync(fresh.ApplicationId, "Rehost", Amanjeet))).Message);
        var sim = await planning.RunSimulationAsync(1, "Rehost", Amanjeet);
        await planning.DiscardSimulationAsync(sim.SimulationId, Amanjeet);
        await using var ctx = db.CreateContext();
        Assert.False(await ctx.Simulations.AnyAsync(s => s.SimulationId == sim.SimulationId));
        Assert.False(await ctx.SimulationImpacts.AnyAsync(s => s.SimulationId == sim.SimulationId));
        Assert.Contains("already has a plan", (await Assert.ThrowsAsync<UserFacingException>(() => planning.DiscardSimulationAsync(1, Amanjeet))).Message);   // SIM-001 has PLAN-001
    }

    [Fact]
    public async Task Plan_rules_phases_draft_submit_and_lock()
    {
        var planning = Planning;
        var sim = await planning.RunSimulationAsync(3, "Rebuild", Amanjeet);   // Payment Gateway
        var all = await planning.PlansAsync();
        Assert.Equal("Plan name must be 5-80 characters", PlanningService.PlanName("Plan", all, null));
        Assert.Equal("Plan name must be 5-80 characters", PlanningService.PlanName(new string('x', 81), all, null));
        Assert.Equal("A plan with this name already exists", PlanningService.PlanName("online store replatform plan", all, null));
        Assert.Equal("Max 500 characters", PlanningService.PlanNotes(new string('n', 501)));
        Assert.Equal("Payment Gateway Rebuild Plan", PlanningService.DefaultPlanName(sim));

        var draft = await planning.CreatePlanAsync(sim.SimulationId, PlanningService.DefaultPlanName(sim), "Compare with PLAN-002", submit: false, Amanjeet);
        Assert.Equal(("Draft", (DateOnly?)null), (draft.Status, draft.SubmittedAt));
        Assert.Equal(EmdtCalc.Phases(sim.EffortWeeks).Select(p => (p.No, p.Name, p.Weeks)), draft.PlanPhases.OrderBy(p => p.PhaseNo).Select(p => (p.PhaseNo, p.PhaseName, p.Weeks)));
        Assert.Equal(sim.EffortWeeks, draft.PlanPhases.Sum(p => p.Weeks));
        Assert.Contains("already has a plan", (await Assert.ThrowsAsync<UserFacingException>(() => planning.CreatePlanAsync(sim.SimulationId, "Another Name Plan", null, false, Amanjeet))).Message);
        Assert.Contains("Only the Architect who created this plan", (await Assert.ThrowsAsync<UserFacingException>(() => planning.UpdateDraftAsync(draft.PlanId, "Changed Name", null, false, Isha))).Message);

        var submitted = await planning.UpdateDraftAsync(draft.PlanId, "Payment Gateway Rebuild Plan v2", "Final", submit: true, Amanjeet);
        Assert.Equal("Submitted", submitted.Status);
        Assert.NotNull(submitted.SubmittedAt);
        Assert.Contains("read-only", (await Assert.ThrowsAsync<UserFacingException>(() => planning.UpdateDraftAsync(draft.PlanId, "Changed Again", null, false, Amanjeet))).Message);

        await using var ctx = db.CreateContext();
        Assert.Equal(["Plan Saved as Draft", "Plan Submitted"], await ctx.AuditLogs.Where(a => a.Target == draft.Code).OrderBy(a => a.AuditLogId).Select(a => a.Action).ToListAsync());
    }

    [Fact]
    public async Task Executive_decisions_follow_the_status_rules()
    {
        var planning = Planning;
        Assert.Equal("Please give a reason (at least 5 characters) to reject", (await Assert.ThrowsAsync<UserFacingException>(() => planning.DecideAsync(1, false, "no", Shivanand))).Message);
        Assert.Equal("Plan not found.", (await Assert.ThrowsAsync<UserFacingException>(() => planning.DecideAsync(6, true, null, Shivanand))).Message);   // PLAN-006 is a Draft

        var approved = await planning.DecideAsync(1, true, null, Shivanand);
        Assert.Equal(("Approved", "Approved.", (int?)Shivanand), (approved.Status, approved.ExecutiveComment, approved.DecidedBy));
        Assert.NotNull(approved.DecidedAt);
        Assert.Contains("was already approved", (await Assert.ThrowsAsync<UserFacingException>(() => planning.DecideAsync(1, false, "Changed my mind", Shivanand))).Message);

        var rejected = await planning.DecideAsync(3, false, "Not this quarter", Shivanand);
        Assert.Equal(("Rejected", "Not this quarter"), (rejected.Status, rejected.ExecutiveComment));

        var list = await planning.ExecutivePlansAsync();
        Assert.DoesNotContain(list, p => p.Status == "Draft");
        await using var ctx = db.CreateContext();
        Assert.Equal(["Plan Approved", "Plan Rejected"],
            await ctx.AuditLogs.Where(a => a.UserId == Shivanand && a.AuditLogId > 8 && (a.Target == "PLAN-001" || a.Target == "PLAN-003")).OrderBy(a => a.AuditLogId).Select(a => a.Action).ToListAsync());
    }

    [Fact]
    public async Task Two_executives_cannot_both_decide_the_same_plan()
    {
        var planning = Planning;
        var results = await Task.WhenAll(
            Task.Run(async () => { try { await planning.DecideAsync(2, true, "Yes", Shivanand); return "ok"; } catch (UserFacingException e) { return e.Message; } }),
            Task.Run(async () => { try { await planning.DecideAsync(2, false, "No, too risky", 3); return "ok"; } catch (UserFacingException e) { return e.Message; } }));
        Assert.Single(results, r => r == "ok");
        Assert.Single(results, r => r.Contains("was already"));
    }
}
