using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Modernization;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// Architect (EMDT): dashboard, applications (read-only), assessments, simulations, create plan, my plans,
/// digital twin (modernization/js/architect-dashboard.js, applications.js, assessments.js, simulation.js,
/// create-plan.js, plans.js).
/// </summary>
public sealed class ArchitectMenu(ConsoleUi ui, MenuRunner runner, ProfileMenu profile, ModernizationService emdt, PlanningService planning,
    ModernizationScreens screens, PlanScreens planUi)
{
    private const int PageSize = 10;

    public Task RunAsync(Session session) =>
        runner.RunAsync(session, "Architect · Application Modernization", async me =>
        {
            var ent = await screens.CurrentAsync(session);
            ui.WriteLine($"Current enterprise: {ent.Name} ({ent.Code})");
            return (IReadOnlyList<MenuItem>)
            [
                new("Dashboard", DashboardAsync),
                new("Choose current enterprise", _ => screens.ChooseEnterpriseAsync(session)),
                new("Applications (read-only)", m => ApplicationsAsync(session, m)),
                new("Assessments", m => AssessAsync(session, m, null)),
                new("Simulations", m => SimulateAsync(session, m, null)),
                new("Create Plan", m => CreatePlanAsync(session, m, null)),
                new("My Plans", m => MyPlansAsync(session, m)),
                new("Digital Twin", _ => screens.DigitalTwinAsync(session)),
                .. profile.CommonItems(session),
            ];
        }, topLevel: true);

    /* ------------------------------------------------------------------ dashboard */

    private async Task DashboardAsync(User me)
    {
        var ents = await emdt.EnterprisesAsync();
        var asms = await planning.AssessmentsAsync();
        var sims = await planning.SimulationsAsync();
        var plans = await planning.PlansAsync();
        var assessedIds = asms.Select(a => a.ApplicationId).ToHashSet();
        var apps = ents.SelectMany(e => e.Applications).ToList();
        ui.Section("Architect Dashboard");
        ui.KeyValues([
            ("Enterprises", ents.Count.ToString()), ("Applications", apps.Count.ToString()),
            ("Assessed Applications", apps.Count(a => assessedIds.Contains(a.ApplicationId)).ToString()),
            ("Submitted Plans", plans.Count(p => p.Status == "Submitted").ToString()),
        ]);
        ui.Table(["Enterprise", "ID", "Category", "Applications", "Assessed", "Pending", "Plans"],
            ents.Select(e =>
            {
                var r = ModernizationService.EmsiOf(e);
                var assessed = e.Applications.Count(a => assessedIds.Contains(a.ApplicationId));
                return (IReadOnlyList<string>)[e.Name, e.Code ?? "", r is null ? "Not classified" : $"{r.Category.Name} (EMSI {Fmt.Dec2(r.Score)})", e.Applications.Count.ToString(),
                    $"{assessed} / {e.Applications.Count}", (e.Applications.Count - assessed).ToString(), plans.Count(p => p.Simulation.Application.EnterpriseId == e.EnterpriseId).ToString()];
            }).ToList(), 3, 5, 6);
        ui.WriteLine("Recent assessments:");
        if (asms.Count == 0) ui.WriteLine("  No assessments yet.");
        else ui.Table(["ID", "Application", "Technical Debt", "Governance Risk", "Date"],
            asms.Take(5).Select(a => (IReadOnlyList<string>)[a.Code ?? "", a.Application.Name, a.TechnicalDebt.ToString(), a.GovernanceRisk.ToString(), Fmt.Date(a.AssessedAt)]).ToList(), 2, 3);
        ui.WriteLine("Recent simulations:");
        if (sims.Count == 0) ui.WriteLine("  No simulations yet.");
        else ui.Table(["ID", "Application", "Strategy", "Risk", "Effort"],
            sims.Take(5).Select(s => (IReadOnlyList<string>)[s.Code ?? "", s.Application.Name, s.Strategy, $"{s.ModernizationRisk} {s.RiskLevel}", s.EffortWeeks + " wk"]).ToList());
        var mine = plans.Where(p => p.CreatedBy == me.UserId).Take(5).ToList();
        ui.WriteLine("My plans:");
        if (mine.Count == 0) ui.WriteLine("  No plans yet.");
        else ui.Table(["Plan", "ID", "Strategy", "Status", "Executive Comment"],
            mine.Select(p => (IReadOnlyList<string>)[p.PlanName, p.Code ?? "", p.Simulation.Strategy, p.Status, Fmt.Cut(p.ExecutiveComment ?? "-", 40)]).ToList());
    }

    /* ------------------------------------------------------------------ applications (read-only) */

    private async Task ApplicationsAsync(Session session, User me)
    {
        string? search = null, assessed = null;
        var page = 1;
        while (true)
        {
            var ent = await screens.CurrentAsync(session);
            var apps = await emdt.ApplicationsAsync(ent.EnterpriseId);
            var plans = await planning.PlansAsync();
            var r = ModernizationService.EmsiOf(ent);
            ui.Section($"Applications - {ent.Name} ({ent.Code})");
            ui.WriteLine("Read-only list. Choose an application to assess, then simulate.");
            ui.WriteLine(r is null ? $"{ent.Name} is not classified yet. Ask the Admin to complete the classification." : $"EMSI {Fmt.Dec2(r.Score)} · {r.Category.Name}");
            ui.KeyValues([
                ("Total Applications", apps.Count.ToString()), ("Pending Assessment", apps.Count(a => a.Assessments.Count == 0).ToString()),
                ("Assessed Applications", apps.Count(a => a.Assessments.Count > 0).ToString()),
                ("Submitted Plans", plans.Count(p => p.Simulation.Application.EnterpriseId == ent.EnterpriseId && p.Status != "Draft").ToString()),
            ]);
            var rows = apps.Where(a => search is null || $"{a.Name} {a.Code}".Contains(search, StringComparison.OrdinalIgnoreCase))
                .Where(a => assessed is null || (assessed == "Assessed") == (a.Assessments.Count > 0)).ToList();
            var p = Paged<Application>.FromList(rows, page, PageSize);
            page = p.Page;
            ui.WriteLine($"Search: {search ?? "(none)"} · Assessment: {assessed ?? "All"}");
            ui.Table(["#", "ID", "Application", "Technology", "Criticality", "Status", "Assessment"],
                p.Items.Select((a, i) => (IReadOnlyList<string>)[(i + 1).ToString(), a.Code ?? "", a.Name, a.Technology, a.Criticality, a.Status, a.Assessments.Count > 0 ? "Assessed" : "Not Assessed"]).ToList(), 0);
            ui.WriteLine(ListNav.Footer(p, "applications"));
            ui.WriteLine("Type a row number to view / assess / simulate, or: S search · A assessment filter · C clear · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, p.Pages)) continue;
            var row = ListNav.RowIndex(answer, p.Items.Count);
            if (row >= 0)
            {
                var app = p.Items[row];
                await runner.SafeRunAsync(async () =>
                {
                    await screens.ShowApplicationAsync(app.ApplicationId);
                    ui.WriteLine($"  1. {(app.Assessments.Count > 0 ? "Re-assess" : "Assess")}");
                    ui.WriteLine(app.Assessments.Count > 0 ? "  2. Simulate" : "  2. Simulate (assess the application first)");
                    ui.WriteLine("  0. Back");
                    switch (ui.Ask("Choose: "))
                    {
                        case "1": await AssessAsync(session, me, app.ApplicationId); break;
                        case "2": await SimulateAsync(session, me, app.ApplicationId); break;
                    }
                });
                if (session.Ended) return;
                continue;
            }
            switch (answer.ToUpperInvariant())
            {
                case "S": var s = ui.Ask("Search name or ID (Enter = no search): "); search = s.Length == 0 ? null : s; page = 1; break;
                case "A": var a2 = ui.AskChoice("Assessment", ["All", "Assessed", "Not Assessed"]); assessed = a2 == "All" ? null : a2; page = 1; break;
                case "C": search = assessed = null; page = 1; break;
                default: ui.Error("Unknown choice. Type a row number, S, A, C, N, P or 0."); break;
            }
        }
    }

    /// <summary>Applications grouped by enterprise (the "app-select" list of the prototype).</summary>
    private async Task<Application?> PickApplicationAsync(Session session, Func<Application, bool> filter, string label)
    {
        var ents = await emdt.EnterprisesAsync();
        var all = new List<Application>();
        foreach (var e in ents) all.AddRange((await emdt.ApplicationsAsync(e.EnterpriseId)).Where(filter));
        if (all.Count == 0) return null;
        var labels = all.Select(a => $"{a.Name} ({a.Code}, {ents.First(e => e.EnterpriseId == a.EnterpriseId).Name})").ToList();
        var current = all.FirstOrDefault(a => a.EnterpriseId == session.EnterpriseId) ?? all[0];
        return all[labels.IndexOf(ui.AskChoice(label, labels, labels[all.IndexOf(current)]))];
    }

    /* ------------------------------------------------------------------ assessments */

    private async Task AssessAsync(Session session, User me, int? applicationId)
    {
        var app = applicationId is null ? await PickApplicationAsync(session, _ => true, "Application") : await emdt.ApplicationAsync(applicationId.Value);
        if (app is null) throw new UserFacingException("No applications yet. Ask the Admin to register applications.");
        session.EnterpriseId = app.EnterpriseId;
        while (true)
        {
            var a = (await emdt.ApplicationAsync(app.ApplicationId))!;
            var links = await emdt.LinksAsync(a.EnterpriseId);
            var history = await planning.AssessmentsAsync(a.ApplicationId);
            var latest = history.FirstOrDefault();
            ui.Section($"Assessment - {a.Name} ({a.Code}) · {(latest is null ? "Not Assessed" : "Assessed")}");
            ui.KeyValues([
                ("Application", a.Name), ("Technology & Framework", $"{a.Technology} · {a.Framework}"), ("Framework Support", a.SupportStatus),
                ("Age", a.AgeYears + " years"), ("Business Criticality", a.Criticality), ("Direct Dependencies", EmdtCalc.AppLinks(a.ApplicationId, links).Count.ToString()),
                ("Deployment Type", a.Deployment), ("Enterprise", a.Enterprise.Name),
            ]);
            if (latest is null) ui.WriteLine("Not assessed yet. Run the assessment first. Simulation is enabled only for assessed applications.");
            else
            {
                ui.WriteLine($"Assessed on {Fmt.Date(latest.AssessedAt)} by {latest.AssessedByNavigation.FullName} ({latest.Code})");
                Factors("Technical Debt", latest.TechnicalDebt, latest.AssessmentFactors.Where(f => f.ScoreType == "TechnicalDebt"));
                Factors("Governance Risk", latest.GovernanceRisk, latest.AssessmentFactors.Where(f => f.ScoreType == "GovernanceRisk"));
                ui.WriteLine($"Dependency Impact: {latest.DependencyImpact} / 100 · {EmdtCalc.Level(latest.DependencyImpact)}");
                ui.WriteLine("Assessment complete. Next: choose a strategy and simulate the modernization.");
                ui.WriteLine("History:");
                ui.Table(["ID", "Date", "Technical Debt", "Governance Risk", "Dependency Impact", "By"],
                    history.Select(h => (IReadOnlyList<string>)[h.Code ?? "", Fmt.Date(h.AssessedAt), h.TechnicalDebt.ToString(), h.GovernanceRisk.ToString(), h.DependencyImpact.ToString(), h.AssessedByNavigation.FullName]).ToList(), 2, 3, 4);
            }
            ui.WriteLine($"  1. {(latest is null ? "Run Assessment" : "Recalculate Assessment")}");
            ui.WriteLine(latest is null ? "  2. Continue to Simulation (run the assessment first)" : "  2. Continue to Simulation");
            ui.WriteLine("  0. Back");
            switch (ui.Ask("Choose: "))
            {
                case "0": return;
                case "1":
                    var rec = await planning.RunAssessmentAsync(a.ApplicationId, me.UserId);
                    ui.Ok($"Assessment {rec.Code} saved: TD {rec.TechnicalDebt}, GR {rec.GovernanceRisk}");
                    break;
                case "2":
                    if (latest is null) { ui.Error("Run the assessment first"); break; }
                    await SimulateAsync(session, me, a.ApplicationId);
                    return;
                default: ui.Error("Type 1, 2 or 0."); break;
            }
        }
    }

    private void Factors(string title, int total, IEnumerable<AssessmentFactor> factors)
    {
        ui.WriteLine($"{title}: {total} / 100 · {EmdtCalc.Level(total)}");
        var rows = factors.OrderBy(f => f.AssessmentFactorId).Select(f => (IReadOnlyList<string>)[f.FactorName, f.FactorValue, "+" + f.Points]).ToList();
        rows.Add(["Total", "", total.ToString()]);
        ui.Table(["Factor", "Value", "Points"], rows, 2);
    }

    /* ------------------------------------------------------------------ simulations */

    private async Task SimulateAsync(Session session, User me, int? applicationId)
    {
        while (true)
        {
            var sims = await planning.SimulationsAsync();
            ui.Section("Simulations");
            ui.Table(["#", "ID", "Application", "Strategy", "DI", "Modernization Risk", "Effort", "Cost", "Date", "Plan"],
                sims.Select((s, i) => (IReadOnlyList<string>)[(i + 1).ToString(), s.Code ?? "", s.Application.Name, s.Strategy, s.Assessment.DependencyImpact.ToString(),
                    $"{s.ModernizationRisk} {s.RiskLevel}", s.EffortWeeks + " wk", Fmt.Lakh(s.CostLakh), Fmt.Date(s.CreatedAt), s.Plan?.Code ?? "-"]).ToList(), 0, 4);
            ui.WriteLine("R run a new simulation · type a row number to open a simulation · 0 back");
            var answer = applicationId is null ? ui.Ask("Choose: ") : "R";
            if (answer == "0") return;
            var row = ListNav.RowIndex(answer, sims.Count);
            if (row >= 0) { await runner.SafeRunAsync(() => SimulationResultAsync(session, me, sims[row].SimulationId, justRun: false)); continue; }
            if (!answer.Equals("R", StringComparison.OrdinalIgnoreCase)) { ui.Error("Type R, a row number or 0."); continue; }

            await runner.SafeRunAsync(async () =>
            {
                var app = applicationId is null
                    ? await PickApplicationAsync(session, a => a.Assessments.Count > 0, "Assessed application")
                    : await emdt.ApplicationAsync(applicationId.Value);
                if (app is null) throw new UserFacingException("No assessed applications yet. Assess an application first.");
                var asm = await planning.LatestAssessmentAsync(app.ApplicationId) ?? throw new UserFacingException("That application is not assessed yet - assess it first");
                var ent = await emdt.EnterpriseAsync(app.EnterpriseId);
                var r = ent is null ? null : ModernizationService.EmsiOf(ent);
                var links = await emdt.LinksAsync(app.EnterpriseId);
                ui.Section($"Simulation - {app.Name} ({app.Code})");
                ui.KeyValues([
                    ("Technical Debt", $"{asm.TechnicalDebt} · {EmdtCalc.Level(asm.TechnicalDebt)}"), ("Governance Risk", $"{asm.GovernanceRisk} · {EmdtCalc.Level(asm.GovernanceRisk)}"),
                    ("Direct Dependencies", EmdtCalc.AppLinks(app.ApplicationId, links).Count.ToString()), ("Business Criticality", app.Criticality),
                    ("EMSI", r is null ? "Not classified" : $"{Fmt.Dec2(r.Score)} · {r.Category.Name}"),
                ]);
                var labels = EmdtCalc.Strategies.Select(s => $"{s.Name} - {s.Label}").ToList();
                var picked = ui.AskChoice("Modernization strategy", labels, labels[2], "Choose a strategy");   // default Refactor
                var sim = await planning.RunSimulationAsync(app.ApplicationId, EmdtCalc.Strategies[labels.IndexOf(picked)].Name, me.UserId);
                ui.Ok($"Simulation {sim.Code} complete: {sim.RiskLevel} risk");
                await SimulationResultAsync(session, me, sim.SimulationId, justRun: true);
            });
            if (applicationId is not null || session.Ended) return;
        }
    }

    private async Task SimulationResultAsync(Session session, User me, int simulationId, bool justRun)
    {
        var s = await planning.SimulationAsync(simulationId) ?? throw new UserFacingException("Simulation not found.");
        ui.Section($"{s.Code} · {s.Application.Name} · {s.Strategy}");
        ui.KeyValues([
            ("Dependency Impact", $"{s.Assessment.DependencyImpact} · {EmdtCalc.Level(s.Assessment.DependencyImpact)}"), ("Strategy Risk", s.StrategyRisk.ToString()),
            ("Modernization Risk", $"{s.ModernizationRisk} · {s.RiskLevel}"), ("Estimated Effort", $"{s.EffortWeeks} weeks"), ("Estimated Cost", Fmt.Lakh(s.CostLakh)),
        ]);
        planUi.Affected(s);
        ui.WriteLine($"Recommendation: {s.Recommendation}");
        var order = s.SimulationImpacts.OrderBy(x => x.SequenceNo).Select(x => x.AffectedName).Append(s.Application.Name).ToList();
        if (s.SimulationImpacts.Count > 0) ui.WriteLine("Suggested order (most critical dependency first): " + string.Join(" → ", order));
        ui.WriteLine(s.Plan is null ? "  1. Create Plan from this simulation" : $"  1. Open plan {s.Plan.Code}");
        ui.WriteLine(s.Plan is null ? "  2. Discard / Modify (delete this simulation and run again)" : "  2. (Discard not possible: the simulation has a plan)");
        ui.WriteLine("  0. Back");
        switch (ui.Ask("Choose: "))
        {
            case "1":
                if (s.Plan is null) await CreatePlanAsync(session, me, s.SimulationId);
                else await PlanDetailAsync(session, me, s.Plan.PlanId);
                break;
            case "2":
                if (s.Plan is not null) throw new UserFacingException($"{s.Code} already has a plan ({s.Plan.Code}), so it cannot be discarded.");
                await planning.DiscardSimulationAsync(s.SimulationId, me.UserId);
                ui.Info("Simulation discarded. Change the application or strategy and run again.");
                break;
        }
    }

    /* ------------------------------------------------------------------ plans */

    private async Task CreatePlanAsync(Session session, User me, int? simulationId)
    {
        Simulation? sim;
        if (simulationId is null)
        {
            var free = (await planning.SimulationsAsync()).Where(s => s.Plan is null).ToList();
            ui.Section("Create Modernization Plan - choose a simulation");
            if (free.Count == 0) throw new UserFacingException("All simulations already have a plan. Run a new simulation first.");
            ui.Table(["#", "Simulation", "Application", "Strategy", "Risk", "Effort", "Cost"],
                free.Select((s, i) => (IReadOnlyList<string>)[(i + 1).ToString(), s.Code ?? "", s.Application.Name, s.Strategy, $"{s.ModernizationRisk} {s.RiskLevel}", s.EffortWeeks + " wk", Fmt.Lakh(s.CostLakh)]).ToList(), 0);
            var n = ui.AskInt("Simulation number (#), 0 = back", 0, free.Count, $"Type a number from 0 to {free.Count}");
            if (n == 0) return;
            sim = free[n - 1];
        }
        else sim = await planning.SimulationAsync(simulationId.Value) ?? throw new UserFacingException("Simulation not found.");
        if (sim.Plan is not null) { await PlanDetailAsync(session, me, sim.Plan.PlanId); return; }

        ui.Section($"Create Plan - from {sim.Code}");
        planUi.Summary(sim);
        planUi.Snapshot(sim);
        ui.WriteLine($"System recommendation: {sim.Recommendation}");
        planUi.Affected(sim);
        ui.WriteLine("(The phase weeks are calculated from the effort and cannot be edited.)");
        planUi.Phases(PlanScreens.PhasesOf(sim.EffortWeeks), sim.EffortWeeks);
        ui.WriteLine("Type 'cancel' at any question to go back. Nothing is saved until you choose Save or Submit.");
        var all = await planning.PlansAsync();
        var name = ui.AskValid("Plan name", v => PlanningService.PlanName(v, all, null), PlanningService.DefaultPlanName(sim));
        var notes = ui.AskValid("Notes (optional, max 500)", PlanningService.PlanNotes, "");
        ui.WriteLine("  1. Save as Draft");
        ui.WriteLine("  2. Submit for approval");
        ui.WriteLine("  0. Cancel");
        switch (ui.Ask("Choose: "))
        {
            case "1":
                var draft = await planning.CreatePlanAsync(sim.SimulationId, name, notes, submit: false, me.UserId);
                ui.Ok($"{draft.Code} saved as draft");
                break;
            case "2":
                if (!ConfirmSubmit()) { ui.Info("Not submitted. Nothing was saved."); return; }
                var sub = await planning.CreatePlanAsync(sim.SimulationId, name, notes, submit: true, me.UserId);
                ui.Ok($"{sub.Code} submitted for approval");
                break;
            default: ui.Info("Cancelled. Nothing was saved."); break;
        }
    }

    /// <summary>"Submit modernization plan?" - the one confirmation before the plan is locked.</summary>
    private bool ConfirmSubmit()
    {
        ui.WriteLine("After submission, this plan will be locked and sent to the Executive for review.");
        return ui.AskYesNo("Submit modernization plan?");
    }

    private async Task MyPlansAsync(Session session, User me)
    {
        string? status = null, search = null;
        while (true)
        {
            var mine = (await planning.PlansAsync()).Where(p => p.CreatedBy == me.UserId).ToList();
            ui.Section("My Plans");
            ui.WriteLine(string.Join(" · ", PlanningService.Statuses.Select(s => $"{s} {mine.Count(p => p.Status == s)}")));
            var rows = mine.Where(p => (status is null || p.Status == status) && (search is null || $"{p.PlanName} {p.Simulation.Application.Name} {p.Code}".Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
            ui.WriteLine($"Status: {status ?? "All"} · Search: {search ?? "(none)"}");
            ui.Table(["#", "Plan ID", "Plan Name", "Application", "Strategy", "Status", "Date", "Executive Comment"],
                rows.Select((p, i) => (IReadOnlyList<string>)[(i + 1).ToString(), p.Code ?? "", p.PlanName, p.Simulation.Application.Name, p.Simulation.Strategy, p.Status,
                    Fmt.Date(p.SubmittedAt ?? p.CreatedAt), Fmt.Cut(p.ExecutiveComment ?? "-", 36)]).ToList(), 0);
            ui.WriteLine("Type a row number to view (Drafts can be edited and submitted), or: T status filter · S search · C clear · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            var row = ListNav.RowIndex(answer, rows.Count);
            if (row >= 0) { await runner.SafeRunAsync(() => PlanDetailAsync(session, me, rows[row].PlanId)); if (session.Ended) return; continue; }
            switch (answer.ToUpperInvariant())
            {
                case "T": var t = ui.AskChoice("Status", ["All", .. PlanningService.Statuses]); status = t == "All" ? null : t; break;
                case "S": var s = ui.Ask("Search plan, application or ID (Enter = no search): "); search = s.Length == 0 ? null : s; break;
                case "C": status = search = null; break;
                default: ui.Error("Unknown choice. Type a row number, T, S, C or 0."); break;
            }
        }
    }

    private async Task PlanDetailAsync(Session session, User me, int planId)
    {
        var p = await planning.PlanAsync(planId) ?? throw new UserFacingException("Plan not found.");
        var editable = p.Status == "Draft" && p.CreatedBy == me.UserId;
        ui.Section($"{p.PlanName} ({p.Code}) · {p.Status}");
        ui.WriteLine(editable ? "Draft plan - you can edit the name and notes, then submit." : "This plan is read-only.");
        planUi.Decision(p);
        planUi.Summary(p.Simulation);
        ui.KeyValues([
            ("Plan ID", p.Code ?? ""), ("Created From", p.Simulation.Code ?? ""), ("Created By", p.CreatedByNavigation.FullName), ("Created On", Fmt.Date(p.CreatedAt)),
            ("Submitted On", Fmt.Date(p.SubmittedAt)), ("Notes", p.Notes ?? "-"),
        ]);
        planUi.Affected(p.Simulation);
        planUi.Phases(PlanScreens.PhasesOf(p), p.Simulation.EffortWeeks);
        if (!editable) return;
        ui.WriteLine("  1. Edit name and notes (stays Draft)");
        ui.WriteLine("  2. Submit for approval");
        ui.WriteLine("  0. Back");
        var c = ui.Ask("Choose: ");
        if (c is not ("1" or "2")) return;
        var all = await planning.PlansAsync();
        var name = ui.AskValid("Plan name", v => PlanningService.PlanName(v, all, p.PlanId), p.PlanName);
        var notes = ui.AskValid("Notes (optional, max 500)", PlanningService.PlanNotes, p.Notes ?? "");
        if (c == "2" && !ConfirmSubmit()) { ui.Info("Not submitted. Nothing was saved."); return; }
        var saved = await planning.UpdateDraftAsync(p.PlanId, name, notes, submit: c == "2", me.UserId);
        ui.Ok(c == "2" ? $"{saved.Code} submitted for approval" : $"{saved.Code} saved as draft");
    }
}
