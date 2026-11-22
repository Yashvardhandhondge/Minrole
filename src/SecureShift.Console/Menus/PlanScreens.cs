using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Modernization;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>Plan / simulation detail blocks shown to both the Architect (create-plan.js) and the Executive (review-plan.js).</summary>
public sealed class PlanScreens(ConsoleUi ui)
{
    /// <summary>The 8 summary boxes of a simulation.</summary>
    public void Summary(Simulation s)
    {
        ui.KeyValues([
            ("Application", s.Application.Name), ("Strategy", s.Strategy), ("Technical Debt", s.Assessment.TechnicalDebt.ToString()),
            ("Governance Risk", s.Assessment.GovernanceRisk.ToString()), ("Dependency Impact", s.Assessment.DependencyImpact.ToString()),
            ("Modernization Risk", $"{s.ModernizationRisk} · {s.RiskLevel}"), ("Estimated Effort", $"{s.EffortWeeks} weeks"), ("Estimated Cost", Fmt.Lakh(s.CostLakh)),
        ]);
    }

    /// <summary>"Simulation snapshot" (frozen numbers the plan was made from).</summary>
    public void Snapshot(Simulation s)
    {
        var b = PlanningService.Breakdown(s);
        ui.WriteLine($"Simulation snapshot ({s.Code}, {Fmt.Date(s.CreatedAt)}):");
        ui.Table(["Item", "Value"],
        [
            ["EMSI score / multiplier", s.EmsiScore is null ? "Not classified / x1.00" : $"{Fmt.Dec2(s.EmsiScore.Value)} / x{Fmt.Dec2(EmdtCalc.MultiplierOf(s.EmsiScore))} ({EmdtCalc.CategoryNameOf(s.EmsiScore)})"],
            ["Technical Debt", s.Assessment.TechnicalDebt.ToString()], ["Governance Risk", s.Assessment.GovernanceRisk.ToString()],
            ["Dependency Impact", s.Assessment.DependencyImpact.ToString()], ["Strategy Risk", s.StrategyRisk.ToString()],
            ["Modernization Risk", s.ModernizationRisk.ToString()], ["Base effort", Fmt.Js(b.BaseWeeks) + " wk"], ["Team size", s.TeamSize.ToString()],
            ["Person-months", Fmt.Js(b.PersonMonths)], ["Cost per person-month", Fmt.Lakh(b.PerPersonMonth)], ["Labour cost", Fmt.Lakh(b.Labour)],
            ["Testing cost (10%)", Fmt.Lakh(b.Testing)], ["Tools cost", Fmt.Lakh(b.Tools)], ["Other fixed cost", Fmt.Lakh(b.OtherFixed)],
            ["Contingency", (int)Math.Round(b.ContingencyPct * 100) + "%"], ["Final cost", Fmt.Lakh(s.CostLakh)],
        ]);
    }

    public void Affected(Simulation s)
    {
        var rows = s.SimulationImpacts.OrderBy(x => x.SequenceNo).ToList();
        ui.WriteLine($"Affected systems ({rows.Count}, in the suggested order - most critical dependency first):");
        if (rows.Count == 0) ui.WriteLine("  No direct dependencies - low blast radius.");
        else ui.Table(["#", "System", "Dependency Type", "Criticality", "Owner"],
            rows.Select(x => (IReadOnlyList<string>)[x.SequenceNo.ToString(), x.AffectedName, x.AffectedType, x.Criticality, x.OwnerName ?? "-"]).ToList(), 0);
    }

    /// <summary>The 4 phases (read-only weeks) with the total check "22 = 22 weeks".</summary>
    public void Phases(IReadOnlyList<(int No, string Name, string Pct, int Weeks)> phases, int effort)
    {
        ui.Table(["#", "Phase", "Share", "Weeks"], phases.Select(p => (IReadOnlyList<string>)[p.No.ToString(), p.Name, p.Pct, p.Weeks.ToString()]).ToList(), 0, 3);
        var total = phases.Sum(p => p.Weeks);
        ui.WriteLine(total == effort ? $"Total estimated effort: {total} = {effort} weeks" : $"Total estimated effort: {total} - Does not match effort ({effort} weeks)");
    }

    public static IReadOnlyList<(int No, string Name, string Pct, int Weeks)> PhasesOf(Plan p) =>
        p.PlanPhases.OrderBy(x => x.PhaseNo).Select(x => (x.PhaseNo, x.PhaseName, x.PhaseNo switch { 1 => "15%", 2 => "20%", 3 => "45%", _ => "Remaining" }, x.Weeks)).ToList();

    public static IReadOnlyList<(int No, string Name, string Pct, int Weeks)> PhasesOf(int effort) =>
        EmdtCalc.Phases(effort).Select(x => (x.No, x.Name, x.Pct, x.Weeks)).ToList();

    /// <summary>Executive decision box (create-plan.js decision-card).</summary>
    public void Decision(Plan p)
    {
        if (p.Status is "Approved" or "Rejected")
            ui.WriteLine($"Executive decision: {p.Status} by {p.DecidedByNavigation?.FullName ?? "-"} on {Fmt.Date(p.DecidedAt)} - {p.ExecutiveComment ?? "No comment."}");
        else if (p.Status == "Submitted")
            ui.WriteLine($"Waiting for Executive review. Submitted on {Fmt.Date(p.SubmittedAt)}. The plan is locked.");
    }
}
