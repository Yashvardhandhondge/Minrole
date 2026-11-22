using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Modernization;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// Executive (EMDT): plans awaiting review, review + approve / reject, decision history
/// (modernization/js/executive-dashboard.js, review-plan.js). Drafts are never shown.
/// </summary>
public sealed class ExecutiveMenu(ConsoleUi ui, MenuRunner runner, ProfileMenu profile, PlanningService planning, ModernizationService emdt, PlanScreens planUi)
{
    private const int PageSize = 10;

    public Task RunAsync(Session session) =>
        runner.RunAsync(session, "Executive · Application Modernization", async me =>
        {
            var plans = await planning.ExecutivePlansAsync();
            return (IReadOnlyList<MenuItem>)
            [
                new($"Plans awaiting review ({plans.Count(p => p.Status == "Submitted")})", m => PlansAsync(session, m, "Submitted")),
                new("All plans (filters)", m => PlansAsync(session, m, null)),
                new("Decision history", DecisionHistoryAsync),
                .. profile.CommonItems(session),
            ];
        }, topLevel: true);

    private async Task PlansAsync(Session session, User me, string? status)
    {
        string? risk = null;
        int? entId = null;
        var page = 1;
        while (true)
        {
            var all = await planning.ExecutivePlansAsync();
            var ents = await emdt.EnterprisesAsync();
            ui.Section("Executive Dashboard - Modernization Plans");
            ui.KeyValues([
                ("Awaiting Review", all.Count(p => p.Status == "Submitted").ToString()), ("Approved Plans", all.Count(p => p.Status == "Approved").ToString()),
                ("Rejected Plans", all.Count(p => p.Status == "Rejected").ToString()),
            ]);
            var entCode = ents.FirstOrDefault(e => e.EnterpriseId == entId)?.Code;
            var rows = all.Where(p => (status is null || p.Status == status) && (risk is null || p.RiskLevel == risk) && (entCode is null || p.EnterpriseCode == entCode)).ToList();
            var pg = Paged<VwPlanSummary>.FromList(rows, page, PageSize);
            page = pg.Page;
            ui.WriteLine($"Showing: {status ?? "All"} plans{(risk is null ? "" : $" · {risk} risk")}{(entCode is null ? "" : $" · {ents.First(e => e.Code == entCode).Name}")} ({rows.Count})");
            ui.Table(["#", "Plan", "ID", "Enterprise", "Strategy", "Risk", "Effort", "Cost", "Submitted By", "Status"],
                pg.Items.Select((p, i) => (IReadOnlyList<string>)[(i + 1).ToString(), Fmt.Cut(p.PlanName, 34), p.PlanCode ?? "", p.EnterpriseName ?? "", p.Strategy ?? "",
                    $"{p.ModernizationRisk} {p.RiskLevel}", p.EffortWeeks + " wk", Fmt.Lakh(p.CostLakh ?? 0), $"{p.CreatedByName} {Fmt.Date(p.SubmittedAt)}", p.Status ?? ""]).ToList(), 0);
            ui.WriteLine(ListNav.Footer(pg, "plans"));
            ui.WriteLine("Type a row number to review / view a plan, or: T status · R risk · E enterprise · C clear · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, pg.Pages)) continue;
            var row = ListNav.RowIndex(answer, pg.Items.Count);
            if (row >= 0)
            {
                await runner.SafeRunAsync(() => ReviewAsync(session, me, pg.Items[row].PlanId!.Value));
                if (session.Ended) return;
                continue;
            }
            switch (answer.ToUpperInvariant())
            {
                case "T": var t = ui.AskChoice("Status", ["All", "Submitted", "Approved", "Rejected"]); status = t == "All" ? null : t; page = 1; break;
                case "R": var r = ui.AskChoice("Risk level", ["All", "High", "Medium", "Low"]); risk = r == "All" ? null : r; page = 1; break;
                case "E":
                    var labels = ents.Select(e => $"{e.Name} ({e.Code})").ToList();
                    var picked = ui.AskChoice("Enterprise", ["All", .. labels]);
                    entId = picked == "All" ? null : ents[labels.IndexOf(picked)].EnterpriseId;
                    page = 1;
                    break;
                case "C": status = "Submitted"; risk = null; entId = null; page = 1; break;   // like the prototype's Clear: back to "Submitted"
                default: ui.Error("Unknown choice. Type a row number, T, R, E, C, N, P or 0."); break;
            }
        }
    }

    /// <summary>review-plan.js: everything about the plan, then Approve / Reject when it is Submitted.</summary>
    private async Task ReviewAsync(Session session, User me, int planId)
    {
        var p = await planning.PlanAsync(planId);
        if (p is null || p.Status == "Draft") throw new UserFacingException("Plan not found.");
        var s = p.Simulation;
        ui.Section($"{p.PlanName} · {p.Code} · from {s.Code} · {p.Status}");
        ui.KeyValues([
            ("Technical Debt", s.Assessment.TechnicalDebt.ToString()), ("Governance Risk", s.Assessment.GovernanceRisk.ToString()),
            ("Dependency Impact", s.Assessment.DependencyImpact.ToString()), ("Modernization Risk", $"{s.ModernizationRisk} · {s.RiskLevel}"),
            ("Strategy", s.Strategy), ("Estimated Effort", $"{s.EffortWeeks} weeks"), ("Estimated Cost", Fmt.Lakh(s.CostLakh)),
            ("EMSI Category", $"{EmdtCalc.CategoryNameOf(s.EmsiScore)} x{Fmt.Dec2(EmdtCalc.MultiplierOf(s.EmsiScore))}"),
        ]);
        planUi.Snapshot(s);
        ui.KeyValues([
            ("Application", s.Application.Name), ("Enterprise", s.Application.Enterprise.Name), ("Business Criticality", s.Application.Criticality),
            ("Submitted By", $"{p.CreatedByNavigation.FullName} (Architect)"), ("Submitted On", Fmt.Date(p.SubmittedAt)), ("Source", $"Simulation {s.Code}"),
            ("Executive Comment", p.ExecutiveComment ?? "-"),
        ]);
        ui.WriteLine($"System recommendation: {s.Recommendation}");
        ui.WriteLine($"Architect notes: {p.Notes ?? "No notes provided."}");
        planUi.Affected(s);
        planUi.Phases(PlanScreens.PhasesOf(p), s.EffortWeeks);
        planUi.Decision(p);
        if (p.Status != "Submitted") return;

        ui.WriteLine("  1. Approve");
        ui.WriteLine("  2. Reject (a reason is required)");
        ui.WriteLine("  0. Back");
        var c = ui.Ask("Choose: ");
        if (c is not ("1" or "2")) return;
        var approve = c == "1";
        var comment = ui.AskValid(approve ? "Comment (optional, max 500)" : "Reason for rejection (at least 5 characters)",
            v => v.Length > 500 ? "Max 500 characters" : !approve && v.Length < 5 ? "Please give a reason (at least 5 characters) to reject" : "", "");
        ui.WriteLine("This decision cannot be changed later.");
        if (!ui.AskYesNo($"{(approve ? "Approve" : "Reject")} {p.PlanName} ({Fmt.Lakh(s.CostLakh)}, {s.EffortWeeks} weeks)?")) { ui.Info("No decision was saved."); return; }
        var decided = await planning.DecideAsync(p.PlanId, approve, comment, me.UserId);
        ui.Ok($"{decided.Code} {decided.Status.ToLowerInvariant()}");
    }

    private async Task DecisionHistoryAsync(User me)
    {
        var decided = (await planning.ExecutivePlansAsync()).Where(p => p.Status is "Approved" or "Rejected").OrderByDescending(p => p.DecidedAt).ThenByDescending(p => p.PlanId).ToList();
        ui.Section("Decision history");
        if (decided.Count == 0)
        {
            ui.WriteLine("No decisions yet.");
            return;
        }
        ui.Table(["Plan ID", "Plan", "Application", "Status", "Decided By", "Decided On", "Comment"],
            decided.Select(p => (IReadOnlyList<string>)[p.PlanCode ?? "", Fmt.Cut(p.PlanName, 34), p.ApplicationName ?? "", p.Status ?? "", p.DecidedByName ?? "-",
                Fmt.Date(p.DecidedAt), Fmt.Cut(p.ExecutiveComment, 36)]).ToList());
    }
}
