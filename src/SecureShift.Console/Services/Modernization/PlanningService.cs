using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Context.Generated;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Services.Modernization;

/// <summary>
/// Architect + Executive (modernization/js/assessments.js, simulation.js, create-plan.js, plans.js,
/// review-plan.js, executive-dashboard.js). Plan status rules (exactly as the prototype):
///   Draft → Submitted (by the Architect who created it; the plan is then locked)
///   Submitted → Approved | Rejected (by an Executive, a reason is required to reject). Nothing else.
/// </summary>
public sealed class PlanningService(IDbContextFactory<SecureShiftDbContext> factory, TimeProvider clock, ModernizationService emdt) : ServiceBase(factory, clock)
{
    public static readonly string[] Statuses = ["Draft", "Submitted", "Approved", "Rejected"];

    /* ------------------------------------------------------------------ assessments */

    public async Task<List<Assessment>> AssessmentsAsync(int? applicationId = null)
    {
        await using var db = await NewDbAsync();
        var q = db.Assessments.AsNoTracking().Include(a => a.Application).Include(a => a.AssessedByNavigation).Include(a => a.AssessmentFactors).AsQueryable();
        if (applicationId is not null) q = q.Where(a => a.ApplicationId == applicationId);
        return await q.OrderByDescending(a => a.AssessedAt).ThenByDescending(a => a.AssessmentId).ToListAsync();
    }

    /// <summary>C.latestAssessment (dev."vw_LatestAssessment").</summary>
    public async Task<Assessment?> LatestAssessmentAsync(int applicationId) => (await AssessmentsAsync(applicationId)).FirstOrDefault();

    /// <summary>"Run Assessment" / "Recalculate Assessment": Technical Debt + Governance Risk + Dependency Impact with their factors.</summary>
    public async Task<Assessment> RunAssessmentAsync(int applicationId, int actorId)
    {
        var app = await emdt.ApplicationAsync(applicationId) ?? throw new UserFacingException("Application not found.");
        var links = await emdt.LinksAsync(app.EnterpriseId);
        var (td, tdFactors) = EmdtCalc.TechDebt(app);
        var (gr, grFactors) = EmdtCalc.GovRisk(app, EmdtCalc.AppLinks(applicationId, links));
        var di = EmdtCalc.DepImpact(applicationId, links);

        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = new Assessment { ApplicationId = applicationId, TechnicalDebt = td, GovernanceRisk = gr, DependencyImpact = di.Score, AssessedBy = actorId, AssessedAt = Today };
        foreach (var f in tdFactors) a.AssessmentFactors.Add(new AssessmentFactor { ScoreType = "TechnicalDebt", FactorName = f.Name, FactorValue = f.Value, Points = f.Points });
        foreach (var f in grFactors) a.AssessmentFactors.Add(new AssessmentFactor { ScoreType = "GovernanceRisk", FactorName = f.Name, FactorValue = f.Value, Points = f.Points });
        db.Assessments.Add(a);
        Audit.Add(db, actorId, "Assessment Run", app.Name, $"Technical Debt {td}, Governance Risk {gr}", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        a.Code ??= $"ASM-{a.AssessmentId:000}";
        return a;
    }

    /* ------------------------------------------------------------------ simulations */

    public async Task<List<Simulation>> SimulationsAsync()
    {
        await using var db = await NewDbAsync();
        return await db.Simulations.AsNoTracking().Include(s => s.Application).ThenInclude(a => a.Enterprise).Include(s => s.Assessment)
            .Include(s => s.Plan).Include(s => s.SimulationImpacts).Include(s => s.CreatedByNavigation)
            .AsSplitQuery().OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.SimulationId).ToListAsync();
    }

    public async Task<Simulation?> SimulationAsync(int simulationId) => (await SimulationsAsync()).FirstOrDefault(s => s.SimulationId == simulationId);

    /// <summary>The full number breakdown of a stored simulation (from its assessment, strategy and EMSI score).</summary>
    public static SimulationCalc Breakdown(Simulation s) =>
        EmdtCalc.Simulate(s.Assessment.TechnicalDebt, s.Assessment.GovernanceRisk, s.Assessment.DependencyImpact, s.Strategy, EmdtCalc.MultiplierOf(s.EmsiScore));

    /// <summary>
    /// "Run Simulation" (C.simulate): uses the LATEST assessment of the application (simulation is enabled only for
    /// assessed applications) and the enterprise EMSI multiplier; the affected systems are stored in migration order.
    /// </summary>
    public async Task<Simulation> RunSimulationAsync(int applicationId, string strategy, int actorId)
    {
        if (!EmdtCalc.Strategies.Any(s => s.Name == strategy)) throw new UserFacingException("Choose a strategy");
        var app = await emdt.ApplicationAsync(applicationId) ?? throw new UserFacingException("Application not found.");
        var asm = await LatestAssessmentAsync(applicationId) ?? throw new UserFacingException("That application is not assessed yet - assess it first");
        var ent = await emdt.EnterpriseAsync(app.EnterpriseId);
        var emsi = ent?.Classification?.EmsiScore;
        var r = EmdtCalc.Simulate(asm.TechnicalDebt, asm.GovernanceRisk, asm.DependencyImpact, strategy, EmdtCalc.MultiplierOf(emsi));
        var links = await emdt.LinksAsync(app.EnterpriseId);
        var names = (await emdt.ApplicationsAsync(app.EnterpriseId)).ToDictionary(a => a.ApplicationId, a => a.Name);
        var order = EmdtCalc.Sequence(EmdtCalc.Affected(applicationId, links, names));

        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var s = new Simulation
        {
            ApplicationId = applicationId, AssessmentId = asm.AssessmentId, Strategy = strategy, StrategyRisk = r.StrategyRisk, EmsiScore = emsi,
            ModernizationRisk = r.ModernizationRisk, EffortWeeks = r.Effort, TeamSize = r.Team, CostLakh = (decimal)r.Cost, Recommendation = r.Recommendation,
            CreatedBy = actorId, CreatedAt = Today,
        };
        var n = 1;
        foreach (var x in order)
            s.SimulationImpacts.Add(new SimulationImpact { SequenceNo = n++, AffectedName = x.Name, AffectedType = x.Type, Criticality = x.Criticality, OwnerName = x.Owner });
        db.Simulations.Add(s);
        Audit.Add(db, actorId, "Simulation Run", app.Name, $"{strategy} · MR {r.ModernizationRisk}", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (await SimulationAsync(s.SimulationId))!;
    }

    /// <summary>"Discard / Modify": removes the simulation that was just run (only while it has no plan).</summary>
    public async Task DiscardSimulationAsync(int simulationId, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var s = await db.Simulations.Include(x => x.Application).Include(x => x.Plan).FirstOrDefaultAsync(x => x.SimulationId == simulationId)
                ?? throw new UserFacingException("Simulation not found.");
        if (s.Plan is not null) throw new UserFacingException($"{s.Code} already has a plan ({s.Plan.Code}), so it cannot be discarded.");
        db.Simulations.Remove(s);   // its SimulationImpacts are removed by ON DELETE CASCADE
        Audit.Add(db, actorId, "Simulation Discarded", s.Code, s.Application.Name, UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /* ------------------------------------------------------------------ plans */

    public async Task<List<Plan>> PlansAsync()
    {
        await using var db = await NewDbAsync();
        return await db.Plans.AsNoTracking()
            .Include(p => p.Simulation).ThenInclude(s => s.Application).ThenInclude(a => a.Enterprise)
            .Include(p => p.Simulation).ThenInclude(s => s.Assessment)
            .Include(p => p.Simulation).ThenInclude(s => s.SimulationImpacts)
            .Include(p => p.PlanPhases).Include(p => p.CreatedByNavigation).Include(p => p.DecidedByNavigation)
            .AsSplitQuery().OrderByDescending(p => p.PlanId).ToListAsync();
    }

    public async Task<Plan?> PlanAsync(int planId) => (await PlansAsync()).FirstOrDefault(p => p.PlanId == planId);

    /// <summary>The Executive list (dev."vw_PlanSummary"): every plan except drafts.</summary>
    public async Task<List<VwPlanSummary>> ExecutivePlansAsync()
    {
        await using var db = await NewDbAsync();
        return await db.VwPlanSummaries.AsNoTracking().Where(p => p.Status != "Draft").OrderByDescending(p => p.SubmittedAt).ThenByDescending(p => p.PlanId).ToListAsync();
    }

    /// <summary>create-plan.js planRules(): name 5-80 characters and unique, notes max 500.</summary>
    public static string PlanName(string v, IEnumerable<Plan> all, int? exceptPlanId)
    {
        if (v.Length is < 5 or > 80) return "Plan name must be 5-80 characters";
        return all.Any(p => p.PlanId != exceptPlanId && p.PlanName.Equals(v, StringComparison.OrdinalIgnoreCase)) ? "A plan with this name already exists" : "";
    }

    public static string PlanNotes(string v) => v.Length <= 500 ? "" : "Max 500 characters";

    /// <summary>C.newPlanFromSim: "&lt;application&gt; &lt;strategy&gt; Plan".</summary>
    public static string DefaultPlanName(Simulation s) => $"{s.Application.Name} {s.Strategy} Plan";

    /// <summary>New plan from a simulation, saved as Draft or Submitted. The 4 phases come from C.phases(effort).</summary>
    public async Task<Plan> CreatePlanAsync(int simulationId, string name, string? notes, bool submit, int actorId)
    {
        name = name.Trim();
        notes = (notes ?? "").Trim();
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var sim = await db.Simulations.Include(s => s.Application).Include(s => s.Plan).FirstOrDefaultAsync(s => s.SimulationId == simulationId)
                  ?? throw new UserFacingException("Simulation not found.");
        if (sim.Plan is not null) throw new UserFacingException($"{sim.Code} already has a plan ({sim.Plan.Code}). Open it from My Plans.");
        var all = await db.Plans.AsNoTracking().ToListAsync();
        Validation.ThrowIfAny(("Plan name", PlanName(name, all, null)), ("Notes", PlanNotes(notes)));

        var phases = EmdtCalc.Phases(sim.EffortWeeks);
        if (phases.Sum(p => p.Weeks) != sim.EffortWeeks) throw new UserFacingException("Phase weeks must equal the estimated effort");
        var plan = new Plan
        {
            SimulationId = sim.SimulationId, PlanName = name, Notes = notes.Length == 0 ? null : notes, Status = submit ? "Submitted" : "Draft",
            CreatedBy = actorId, CreatedAt = Today, SubmittedAt = submit ? Today : null,
        };
        foreach (var p in phases) plan.PlanPhases.Add(new PlanPhase { PhaseNo = p.No, PhaseName = p.Name, Weeks = p.Weeks });
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        Audit.Add(db, actorId, submit ? "Plan Submitted" : "Plan Saved as Draft", $"PLAN-{plan.PlanId:000}", sim.Application.Name, UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (await PlanAsync(plan.PlanId))!;
    }

    /// <summary>Edit a Draft (name, notes) and keep it as Draft or submit it. Only the Architect who created it.</summary>
    public async Task<Plan> UpdateDraftAsync(int planId, string name, string? notes, bool submit, int actorId)
    {
        name = name.Trim();
        notes = (notes ?? "").Trim();
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var plan = await LockPlanAsync(db, planId);
        if (plan.Status != "Draft") throw new UserFacingException($"{plan.Code} is {plan.Status} and read-only. Only Draft plans can be edited.");
        if (plan.CreatedBy != actorId) throw new UserFacingException("Only the Architect who created this plan can edit it.");
        var all = await db.Plans.AsNoTracking().ToListAsync();
        Validation.ThrowIfAny(("Plan name", PlanName(name, all, planId)), ("Notes", PlanNotes(notes)));
        plan.PlanName = name;
        plan.Notes = notes.Length == 0 ? null : notes;
        if (submit)
        {
            plan.Status = "Submitted";
            plan.SubmittedAt = Today;
        }
        var app = await db.Simulations.Where(s => s.SimulationId == plan.SimulationId).Select(s => s.Application.Name).SingleAsync();
        Audit.Add(db, actorId, submit ? "Plan Submitted" : "Plan Saved as Draft", plan.Code, app, UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (await PlanAsync(planId))!;
    }

    /// <summary>
    /// review-plan.js decide(): only a Submitted plan; a reason of at least 5 characters to reject; comment max 500.
    /// "This decision cannot be changed later." The row is locked (SELECT ... FOR UPDATE) so two Executives
    /// cannot decide the same plan at the same time.
    /// </summary>
    public async Task<Plan> DecideAsync(int planId, bool approve, string? comment, int actorId)
    {
        comment = (comment ?? "").Trim();
        if (!approve && comment.Length < 5) throw new UserFacingException("Please give a reason (at least 5 characters) to reject");
        if (comment.Length > 500) throw new UserFacingException("Max 500 characters");
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var plan = await LockPlanAsync(db, planId);
        if (plan.Status != "Submitted")
            throw new UserFacingException(plan.Status == "Draft" ? "Plan not found." : $"{plan.Code} was already {plan.Status.ToLowerInvariant()}. The decision cannot be changed.");
        plan.Status = approve ? "Approved" : "Rejected";
        plan.ExecutiveComment = comment.Length > 0 ? comment : "Approved.";
        plan.DecidedBy = actorId;
        plan.DecidedAt = Today;
        var app = await db.Simulations.Where(s => s.SimulationId == plan.SimulationId).Select(s => s.Application.Name).SingleAsync();
        Audit.Add(db, actorId, approve ? "Plan Approved" : "Plan Rejected", plan.Code, app, UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (await PlanAsync(planId))!;
    }

    private static async Task<Plan> LockPlanAsync(SecureShiftDbContext db, int planId) =>
        await db.Plans.FromSqlInterpolated($"SELECT * FROM dev.\"Plans\" WHERE \"PlanId\" = {planId} FOR UPDATE").SingleOrDefaultAsync()
        ?? throw new UserFacingException("Plan not found.");
}
