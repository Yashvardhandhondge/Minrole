using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Modernization;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>Screens used by both Admin and Architect: current enterprise, application view, digital twin (text).</summary>
public sealed class ModernizationScreens(ConsoleUi ui, ModernizationService emdt)
{
    /// <summary>SS.EM.entId(): the current enterprise of the session (first enterprise when none is chosen yet).</summary>
    public async Task<Enterprise> CurrentAsync(Session session)
    {
        var all = await emdt.EnterprisesAsync();
        if (all.Count == 0) throw new UserFacingException("No enterprise is registered yet.");
        var e = all.FirstOrDefault(x => x.EnterpriseId == session.EnterpriseId) ?? all[0];
        session.EnterpriseId = e.EnterpriseId;
        return e;
    }

    public async Task ChooseEnterpriseAsync(Session session)
    {
        var all = await emdt.EnterprisesAsync();
        if (all.Count == 0) throw new UserFacingException("No enterprise is registered yet.");
        var labels = all.Select(e => $"{e.Name} ({e.Code})").ToList();
        var current = all.FirstOrDefault(x => x.EnterpriseId == session.EnterpriseId) ?? all[0];
        var picked = ui.AskChoice("Enterprise", labels, $"{current.Name} ({current.Code})");
        session.EnterpriseId = all[labels.IndexOf(picked)].EnterpriseId;
        ui.Ok($"Current enterprise: {picked}");
    }

    public static string Yes(bool b) => b ? "Yes" : "No";

    /// <summary>The "View" popup of applications.js: details, direct dependencies, latest assessment.</summary>
    public async Task ShowApplicationAsync(int applicationId)
    {
        var a = await emdt.ApplicationAsync(applicationId) ?? throw new UserFacingException("Application not found.");
        var links = await emdt.LinksAsync(a.EnterpriseId);
        var apps = (await emdt.ApplicationsAsync(a.EnterpriseId)).ToDictionary(x => x.ApplicationId, x => x.Name);
        var affected = EmdtCalc.Affected(a.ApplicationId, links, apps);
        var latest = a.Assessments.OrderByDescending(x => x.AssessedAt).ThenByDescending(x => x.AssessmentId).FirstOrDefault();
        ui.Section($"{a.Name} ({a.Code})");
        ui.KeyValues([
            ("Enterprise", a.Enterprise.Name), ("Technology", a.Technology), ("Framework", a.Framework), ("Framework support", a.SupportStatus),
            ("Age", a.AgeYears + " years"), ("Deployment", a.Deployment), ("Lines of code", Fmt.Indian(a.LinesOfCode)), ("Business unit", a.BusinessUnit ?? "-"),
            ("Owner team", a.OwnerTeam), ("Status", a.Status), ("Criticality", a.Criticality), ("Sensitive data", Yes(a.HasSensitiveData)),
            ("Security issues", a.SecurityIssues.ToString()), ("Downtime tolerance", a.DowntimeTolerance), ("Compliance policies", a.CompliancePolicies.ToString()),
            ("Documentation", a.DocQuality), ("Code complexity", a.Complexity),
            ("Assessment", latest is null ? "Not assessed" : $"TD {latest.TechnicalDebt} · GR {latest.GovernanceRisk} ({Fmt.Date(latest.AssessedAt)})"),
        ]);
        ui.WriteLine($"Direct dependencies ({affected.Count}):");
        if (affected.Count == 0) ui.WriteLine("  No dependencies mapped.");
        else ui.Table(["System", "Type", "Criticality", "Owner"], affected.Select(x => (IReadOnlyList<string>)[x.Name, x.Type, x.Criticality, x.Owner]).ToList());
    }

    /// <summary>digital-twin.js as text: one block per application with its components and every direct link.</summary>
    public async Task DigitalTwinAsync(Session session)
    {
        var ent = await CurrentAsync(session);
        int? onlyApp = null;
        string? crit = null;
        while (true)
        {
            var apps = await emdt.ApplicationsAsync(ent.EnterpriseId);
            var comps = await emdt.ComponentsAsync(ent.EnterpriseId);
            var links = await emdt.LinksAsync(ent.EnterpriseId);
            var shown = links.Where(l => crit is null || l.Criticality == crit).ToList();

            ui.Section($"Digital Twin - {ent.Name} ({ent.Code})");
            ui.WriteLine($"Landscape Summary: Applications {apps.Count} · Services {comps.Count(c => c.ComponentType == "Service")} · " +
                         $"Databases {comps.Count(c => c.ComponentType == "Database")} · APIs {comps.Count(c => c.ComponentType == "API")} · Links {links.Count}");
            ui.WriteLine($"Filter - application: {apps.FirstOrDefault(a => a.ApplicationId == onlyApp)?.Name ?? "All"} · criticality: {crit ?? "All"}");
            if (apps.Count == 0) ui.WriteLine("No applications registered for this enterprise yet.");
            foreach (var a in apps.Where(a => onlyApp is null || a.ApplicationId == onlyApp))
            {
                var di = EmdtCalc.DepImpact(a.ApplicationId, links);
                ui.WriteLine();
                ui.WriteLine($"[{a.Code}] {a.Name}  (Application · {a.Criticality} · Dependency Impact {di.Score}/100)");
                WriteLinks("  ", shown, l => l.SourceApplicationId == a.ApplicationId, l => l.TargetApplicationId == a.ApplicationId);
                var mine = comps.Where(c => c.ApplicationId == a.ApplicationId).ToList();
                for (var i = 0; i < mine.Count; i++)
                {
                    var c = mine[i];
                    var last = i == mine.Count - 1;
                    ui.WriteLine($"  {(last ? "└─" : "├─")} [{c.Code}] {c.Name} ({c.ComponentType})");
                    WriteLinks(last ? "       " : "  │    ", shown, l => l.SourceComponentId == c.ComponentId, l => l.TargetComponentId == c.ComponentId);
                }
            }
            ui.WriteLine();
            ui.WriteLine("A one application · K criticality filter · C clear · E choose enterprise · 0 back");
            switch (ui.Ask("Choose: ").ToUpperInvariant())
            {
                case "0": return;
                case "A":
                    if (apps.Count == 0) break;
                    var labels = apps.Select(a => $"{a.Name} ({a.Code})").ToList();
                    onlyApp = apps[labels.IndexOf(ui.AskChoice("Application", labels))].ApplicationId;
                    break;
                case "K": var k = ui.AskChoice("Criticality", ["All", .. EmdtCalc.Criticalities]); crit = k == "All" ? null : k; break;
                case "C": onlyApp = null; crit = null; break;
                case "E": await ChooseEnterpriseAsync(session); ent = await CurrentAsync(session); onlyApp = null; break;
                default: ui.Error("Type A, K, C, E or 0."); break;
            }
        }
    }

    private void WriteLinks(string indent, List<Dependency> links, Func<Dependency, bool> isSource, Func<Dependency, bool> isTarget)
    {
        foreach (var l in links.Where(isSource))
            ui.WriteLine($"{indent}depends on → {EmdtCalc.TargetName(l)} ({EmdtCalc.LinkType(l)})  [{l.Criticality}]  {l.Code}");
        foreach (var l in links.Where(isTarget))
            ui.WriteLine($"{indent}used by    ← {EmdtCalc.SourceName(l)}  [{l.Criticality}]  {l.Code}");
    }
}
