using System.Text.Json;
using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Governance;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// Cloud Admin (Cloud Governance): dashboard, companies, Analysis Wizard (3 JSON files), identities,
/// findings, generated roles, rule settings, high-risk patterns, reports, notifications (governance/js/*.js).
/// </summary>
public sealed class CloudAdminMenu(ConsoleUi ui, MenuRunner runner, ProfileMenu profile, GovernanceService gov, AnalysisService analyses, TimeProvider clock)
{
    private const int PageSize = 10;

    public Task RunAsync(Session session) =>
        runner.RunAsync(session, "Cloud Admin · Cloud Governance", async me =>
        {
            var unread = await gov.UnreadCountAsync(me.UserId);
            return (IReadOnlyList<MenuItem>)
            [
                new("Dashboard", DashboardAsync),
                new("Companies", m => CompaniesAsync(session, m)),
                new("Run Analysis (Analysis Wizard)", m => WizardAsync(m, null)),
                new("Previous Analyses", PreviousAsync),
                new("Identities", m => IdentitiesAsync(session, m, null)),
                new("High-Risk Findings", m => FindingsAsync(session, m)),
                new("Generated Roles", m => RolesAsync(session, m)),
                new("Rule Settings", RulesAsync),
                new("High-Risk Patterns", PatternsAsync),
                new("Reports", ReportsAsync),
                new($"Notifications ({unread} unread)", NotificationsAsync),
                .. profile.CommonItems(session),
            ];
        }, topLevel: true);

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /* ------------------------------------------------------------------ dashboard */

    private async Task DashboardAsync(User me)
    {
        var d = await gov.DashboardAsync();
        ui.Section("Governance Dashboard");
        ui.WriteLine($"Welcome back, {me.FullName}");
        if (d.Latest is null)
        {
            ui.WriteLine("No analysis yet. Choose 'Run Analysis (Analysis Wizard)' to analyse the first company.");
            return;
        }
        var a = d.Latest;
        ui.WriteLine($"Latest analysis complete - {a.Company.CompanyName}");
        ui.WriteLine($"{a.IdentityCount} identities analysed from {a.PeriodDays} days of Activity Log ({Period(a)}) · {a.Code} · {Fmt.DateTime(a.RunAt)}");
        ui.KeyValues([
            ("Total Identities", (a.IdentityCount ?? 0).ToString()),
            ("Average Score", Fmt.Dec2(a.AvgScore ?? 0)),
            ("High-Risk Unused", d.OpenFindings.ToString()),
            ("Roles Pending Review", d.PendingRoles.ToString()),
            ("Analysis Period", $"{Period(a)} ({a.PeriodDays} days)"),
        ]);
        ui.Section("Identities by risk");
        var max = Math.Max(d.High, Math.Max(d.Medium, d.Low));
        ui.Table(["Risk", "Identities", ""],
        [
            ["High (70-100)", d.High.ToString(), ConsoleUi.Bar(d.High, max)],
            ["Medium (40-69)", d.Medium.ToString(), ConsoleUi.Bar(d.Medium, max)],
            ["Low (0-39)", d.Low.ToString(), ConsoleUi.Bar(d.Low, max)],
        ], 1);
        ui.Section("Top 5 identities to fix");
        IdentityTable(d.Top5, numbered: false);
        ui.Section($"Average score trend ({a.Company.CompanyName})");
        ui.Table(["Analysis", "Period", "Avg Score", ""],
            d.Trend.Select(t => (IReadOnlyList<string>)[t.Code, Period(t), Fmt.Dec2(t.AvgScore ?? 0), ConsoleUi.Bar((double)(t.AvgScore ?? 0), 100)]).ToList(), 2);
    }

    private static string Period(Analysis a) => $"{Fmt.Date(a.PeriodFrom)} - {Fmt.Date(a.PeriodTo)}";

    private void IdentityTable(IReadOnlyList<VwIdentityRisk> rows, bool numbered)
    {
        var data = rows.Select((r, i) => (IReadOnlyList<string>)[
            numbered ? (i + 1).ToString() : "", Fmt.Cut(r.DisplayName, 28), Fmt.Cut(r.UserName, 24), r.IdentityType ?? "", Fmt.Cut(r.AssignedRole, 22),
            Fmt.Cut(r.ScopeName ?? r.Scope, 16), Fmt.Indian(r.GrantedCount ?? 0), Fmt.Indian(r.UsedCount ?? 0), (r.RiskScore ?? 0).ToString(), r.RiskLevel ?? ""]).ToList();
        if (!numbered) data = data.Select(r => (IReadOnlyList<string>)r.Skip(1).ToList()).ToList();
        string[] headers = ["#", "Identity", "User Name", "Type", "Current Role", "Scope", "Granted", "Used", "Score", "Risk"];
        ui.Table(numbered ? headers : headers[1..], data, numbered ? [0, 6, 7, 8] : [5, 6, 7]);
    }

    /* ------------------------------------------------------------------ companies */

    private async Task CompaniesAsync(Session session, User me)
    {
        string? search = null, risk = null;
        var page = 1;
        while (true)
        {
            var all = await gov.CompaniesAsync();
            var rows = all.Where(c => search is null || c.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Where(c => risk is null || (LatestOf(c) is { } l && GovernanceScoring.RiskBand(l.AvgScore ?? 0) == risk)).ToList();
            var p = Paged<Company>.FromList(rows, page, PageSize);
            page = p.Page;
            ui.Section($"Companies - {all.Count} companies analysed in this workspace");
            ui.WriteLine($"Search: {search ?? "(none)"} · Risk level: {risk ?? "All"}");
            ui.Table(["#", "Company", "Identities", "Avg Score", "High-Risk Unused", "Last Analysis", "Risk Level"],
                p.Items.Select((c, i) =>
                {
                    var l = LatestOf(c);
                    return (IReadOnlyList<string>)[(i + 1).ToString(), c.CompanyName, l is null ? "0" : (l.IdentityCount ?? 0).ToString(), l is null ? "0" : Fmt.Dec2(l.AvgScore ?? 0),
                        l is null ? "0" : (l.HighRiskCount ?? 0).ToString(), l is null ? "Not analysed" : Fmt.Date(DateOnly.FromDateTime(l.RunAt)),
                        l is null ? "PENDING" : GovernanceScoring.RiskBand(l.AvgScore ?? 0)];
                }).ToList(), 0, 2, 3, 4);
            ui.WriteLine(ListNav.Footer(p, "companies"));
            ui.WriteLine("Type a row number to open a company, or: A add company · S search · R risk filter · C clear · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, p.Pages)) continue;
            var row = ListNav.RowIndex(answer, p.Items.Count);
            if (row >= 0)
            {
                await runner.SafeRunAsync(() => CompanyDetailAsync(session, me, p.Items[row].CompanyId));
                if (session.Ended) return;
                continue;
            }
            switch (answer.ToUpperInvariant())
            {
                case "A": await runner.SafeRunAsync(() => AddCompanyAsync(me)); break;
                case "S": var s = ui.Ask("Search company name (Enter = no search): "); search = s.Length == 0 ? null : s; page = 1; break;
                case "R": var r = ui.AskChoice("Risk level", ["All", "HIGH", "MEDIUM", "LOW"]); risk = r == "All" ? null : r; page = 1; break;
                case "C": search = null; risk = null; page = 1; break;
                default: ui.Error("Unknown choice. Type a row number, A, S, R, C, N, P or 0."); break;
            }
        }
    }

    private static Analysis? LatestOf(Company c) => GovernanceService.LatestOf(c);

    private async Task CompanyDetailAsync(Session session, User me, int companyId)
    {
        var c = (await gov.CompaniesAsync()).FirstOrDefault(x => x.CompanyId == companyId) ?? throw new UserFacingException("Company not found.");
        var l = LatestOf(c);
        ui.Section(c.CompanyName);
        ui.KeyValues([
            ("Identities", (l?.IdentityCount ?? 0).ToString()), ("Average Score", Fmt.Dec2(l?.AvgScore ?? 0)), ("High-Risk Unused", (l?.HighRiskCount ?? 0).ToString()),
            ("Last analysis", l is null ? "Not analysed" : $"{l.Code} on {Fmt.DateTime(l.RunAt)}"), ("Risk level", l is null ? "PENDING" : GovernanceScoring.RiskBand(l.AvgScore ?? 0)),
            ("Azure Tenant ID", c.TenantId ?? "-"), ("Subscriptions", c.Subscriptions.ToString()),
        ]);
        ui.WriteLine("  1. Run New Analysis");
        ui.WriteLine("  2. View Identities");
        ui.WriteLine("  0. Back");
        switch (ui.Ask("Choose: "))
        {
            case "1": await WizardAsync(me, companyId); break;
            case "2":
                if (l is null) throw new UserFacingException($"{c.CompanyName} has no analysis yet. Run the Analysis Wizard first.");
                await IdentitiesAsync(session, me, l.AnalysisId);
                break;
        }
    }

    private async Task AddCompanyAsync(User me)
    {
        ui.Section("Add Company Data");
        ui.WriteLine("Type 'cancel' at any question to go back. After adding, run the Analysis Wizard to upload this company's files.");
        var name = ui.AskValid("Company Name", v => v.Length >= 3 ? "" : "Enter at least 3 characters");
        var tenant = ui.AskValid("Azure Tenant ID", v => v.Length >= 8 ? "" : "Enter a valid tenant ID");
        var subs = ui.AskValid("Subscriptions", v => Validation.IsInt(v, 1, 500) ? "" : "At least 1", "1");
        var company = await gov.AddCompanyAsync(name, tenant, subs, me.UserId);
        ui.Ok($"{company.CompanyName} added. Run the Analysis Wizard next.");
    }

    /* ------------------------------------------------------------------ analysis wizard */

    private static string SamplePath(string file)
    {
        var local = Path.Combine("samples", "governance", file);
        return File.Exists(local) ? local : Path.Combine(AppContext.BaseDirectory, "samples", "governance", file);
    }

    private async Task WizardAsync(User me, int? companyId)
    {
        ui.Section("Analysis Wizard - Step 1 of 4: Company, period and files");
        ui.WriteLine("Type 'cancel' at any question to stop. Nothing is saved before step 3.");
        var companies = await gov.CompaniesAsync();
        if (companies.Count == 0) throw new UserFacingException("Add a company first (Companies > A).");
        var company = companyId is null
            ? companies.First(c => c.CompanyName == ui.AskChoice("Company", companies.Select(c => c.CompanyName).ToList()))
            : companies.First(c => c.CompanyId == companyId);
        ui.WriteLine($"Company: {company.CompanyName}");

        // fixed analysis period (From / To), default from Rule Settings
        var rules = await gov.ActiveRulesAsync();
        string[] presets = ["Last 30 days", "Last 60 days", "Last 90 days", "Custom range"];
        var def = rules.DefaultPeriodDays is 30 or 60 or 90 ? $"Last {rules.DefaultPeriodDays} days" : "Last 90 days";
        DateOnly from, to;
        while (true)
        {
            var choice = ui.AskChoice("Analysis period", presets, def);
            if (choice == "Custom range")
            {
                from = ui.AskDate("From", null, allowEmpty: false)!.Value;
                to = ui.AskDate("To", null, allowEmpty: false)!.Value;
            }
            else
            {
                var days = int.Parse(choice.Split(' ')[1]);
                to = Today.AddDays(-1);
                from = to.AddDays(-days + 1);
            }
            var error = GovernanceInputReader.CheckPeriod(from, to, Today);
            if (error.Length == 0) break;
            ui.Error(error);
        }
        ui.WriteLine($"Period: {Fmt.Date(from)} - {Fmt.Date(to)} · {to.DayNumber - from.DayNumber + 1} days");

        ui.WriteLine("Type the path of each JSON file (Enter = the sample file shown in [brackets]).");
        var assignments = AskFile(GovernanceInputReader.AssignmentsTitle, SamplePath("role-assignments.json"));
        var activity = AskFile(GovernanceInputReader.ActivityTitle, SamplePath("activity-log-90d.json"));
        var catalog = AskFile(GovernanceInputReader.CatalogTitle, SamplePath("roles-catalog.json"));

        ui.Section("Analysis Wizard - Step 2 of 4: Validate");
        GovernanceInput input;
        try
        {
            input = await GovernanceInputReader.LoadAsync(assignments, activity, catalog, from, to, company.CompanyName);
        }
        catch (GovernanceInputException ex)
        {
            foreach (var s in ex.Steps) PrintStep(s);
            if (ex.Steps.All(s => s.Ok)) ui.Error(ex.Message);
            ui.WriteLine("Nothing was saved. Fix the file (or choose another one) and run the wizard again.");
            return;
        }
        foreach (var s in input.Steps) PrintStep(s);
        ui.Ok("Validation passed");
        ui.WriteLine(input.Summary);
        if (!ui.AskYesNo("Run the analysis now?"))
        {
            ui.Info("Cancelled. Nothing was saved.");
            return;
        }

        ui.Section("Analysis Wizard - Step 3 of 4: Run");
        var analysis = await analyses.RunAsync(company.CompanyId, input, me.UserId);
        for (var i = 0; i < AnalysisService.Stages.Length; i++)
            ui.WriteLine($"  [{(int)Math.Round((i + 1) * 100.0 / AnalysisService.Stages.Length),3}%] {AnalysisService.Stages[i]} ... done");

        ui.Section("Analysis Wizard - Step 4 of 4: Results");
        var roles = await gov.RolesAsync(analysis.AnalysisId);
        ui.KeyValues([
            ("Analysis ID", analysis.Code), ("Date", Fmt.DateTime(analysis.RunAt)), ("Company", company.CompanyName),
            ("Period", $"{Fmt.Date(from)} - {Fmt.Date(to)} ({input.PeriodDays} days)"), ("Identities analysed", (analysis.IdentityCount ?? 0).ToString()),
            ("Average score", Fmt.Dec2(analysis.AvgScore ?? 0)), ("High-risk unused permissions", (analysis.HighRiskCount ?? 0).ToString()),
            ("Least-privilege roles generated", roles.Count.ToString()),
        ]);
        ui.Ok($"Analysis {analysis.Code} saved. Next: Identities, High-Risk Findings or Generated Roles.");
    }

    private void PrintStep(ValidationStep s)
    {
        if (s.Ok) ui.Ok($"{s.Name} - {s.Message}");
        else ui.Error($"{s.Name} - {s.Message}");
    }

    private string AskFile(string title, string sample)
    {
        while (true)
        {
            var typed = ui.AskField($"{title} [{sample}]: ");
            try
            {
                var full = GovernanceInputReader.ResolvePath(typed.Length == 0 ? sample : typed, title);
                var size = new FileInfo(full).Length;
                ui.WriteLine($"    Uploaded: {Path.GetFileName(full)} ({(size > 1048576 ? (size / 1048576.0).ToString("0.0") + " MB" : Math.Max(1, size / 1024) + " KB")})");
                return full;
            }
            catch (UserFacingException ex)
            {
                ui.Error(ex.Message);
            }
        }
    }

    private async Task PreviousAsync(User me)
    {
        var list = await gov.AnalysesAsync();
        ui.Section("Previous Analyses");
        ui.Table(["Analysis ID", "Company", "Period", "Days", "Completed", "Identities", "Avg Score", "Status"],
            list.Select(a => (IReadOnlyList<string>)[a.Code, a.Company.CompanyName, Period(a), (a.PeriodDays ?? 0).ToString(), Fmt.DateTime(a.RunAt),
                (a.IdentityCount ?? 0).ToString(), Fmt.Dec2(a.AvgScore ?? 0), a.Status]).ToList(), 3, 5, 6);
    }

    /// <summary>Asks which analysis to look at (newest first). Null when there is none.</summary>
    private async Task<Analysis?> PickAnalysisAsync(int? current)
    {
        var list = await gov.AnalysesAsync();
        if (list.Count == 0) return null;
        if (current is null) return list[0];
        var labels = list.Select(a => $"{a.Code} · {a.Company.CompanyName} · {Period(a)}").ToList();
        var picked = ui.AskChoice("Analysis", labels, labels[Math.Max(0, list.FindIndex(a => a.AnalysisId == current))]);
        return list[labels.IndexOf(picked)];
    }

    /* ------------------------------------------------------------------ identities */

    private async Task IdentitiesAsync(Session session, User me, int? analysisId)
    {
        var analysis = analysisId is null ? await PickAnalysisAsync(null) : await gov.AnalysisAsync(analysisId.Value);
        if (analysis is null) throw new UserFacingException("No analysis yet. Run the Analysis Wizard first.");
        string? search = null, type = null, risk = null;
        var page = 1;
        while (true)
        {
            var rows = await gov.IdentitiesAsync(analysis.AnalysisId, search, type, risk);
            var p = Paged<VwIdentityRisk>.FromList(rows, page, PageSize);
            page = p.Page;
            ui.Section($"Identities - {analysis.Code} · {analysis.Company.CompanyName} · {Period(analysis)}");
            ui.WriteLine($"Search: {search ?? "(none)"} · Type: {type ?? "All"} · Risk: {risk ?? "All"}");
            IdentityTable(p.Items, numbered: true);
            ui.WriteLine(ListNav.Footer(p, "identities"));
            ui.WriteLine("Type a row number to open an identity, or: S search · T type · R risk · A other analysis · E export CSV · C clear · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, p.Pages)) continue;
            var row = ListNav.RowIndex(answer, p.Items.Count);
            if (row >= 0)
            {
                await runner.SafeRunAsync(() => IdentityDetailAsync(session, me, analysis.AnalysisId, p.Items[row].IdentityId!.Value));
                if (session.Ended) return;
                continue;
            }
            switch (answer.ToUpperInvariant())
            {
                case "S": var s = ui.Ask("Search name, user name, role or scope (Enter = no search): "); search = s.Length == 0 ? null : s; page = 1; break;
                case "T": var t = ui.AskChoice("Type", ["All", .. GovernanceService.IdentityTypes]); type = t == "All" ? null : t; page = 1; break;
                case "R":
                    var r = ui.AskChoice("Risk", ["All", "High (70-100)", "Medium (40-69)", "Low (0-39)"]);
                    risk = r == "All" ? null : r.Split(' ')[0].ToUpperInvariant();
                    page = 1;
                    break;
                case "A": analysis = await PickAnalysisAsync(analysis.AnalysisId) ?? analysis; page = 1; break;
                case "E":
                    var file = await GovernanceService.SaveExportAsync(null, "identities-export.csv", GovernanceService.IdentitiesCsv(rows));
                    ui.Ok($"Saved {rows.Count} identities to {file}");
                    break;
                case "C": search = type = risk = null; page = 1; break;
                default: ui.Error("Unknown choice. Type a row number, S, T, R, A, E, C, N, P or 0."); break;
            }
        }
    }

    private async Task IdentityDetailAsync(Session session, User me, int analysisId, int identityId)
    {
        var s = await gov.IdentityScoreAsync(analysisId, identityId) ?? throw new UserFacingException("Identity not found in this analysis.");
        var rules = await gov.ActiveRulesAsync();
        var usage = await gov.UsageAsync(analysisId, identityId);
        var unused = await gov.FindingsAsync(analysisId, identityId: identityId);
        var role = (await gov.RolesAsync(analysisId)).FirstOrDefault(r => r.IdentityId == identityId);
        var i = s.Identity;

        ui.Section($"Identity - {i.DisplayName}");
        ui.KeyValues([
            ("Display Name", i.DisplayName), ("User Name", i.UserName), ("Type", i.IdentityType), ("Current Role", i.AssignedRole),
            ("Scope", i.ScopeName is null ? i.Scope : $"{i.Scope} ({i.ScopeName})"), ("Analysis Period", Period(s.Analysis)),
            ("Suggested Scope", s.SuggestedScope is null ? "-" : $"{s.SuggestedScope} (Resource Group)"),
            ("Risk Score", $"{s.RiskScore} / 100 · {s.RiskLevel}"),
        ]);
        ui.Section("Why this score");
        ui.Table(["Signal", "Value", "Weight"],
        [
            ["Unused permission ratio", GovernanceScoring.UnusedRatio(s.GrantedCount, s.UsedCount) + "%", rules.WeightUnused + "%"],
            ["Unused high-risk actions", s.UnusedHighRisk.ToString(), rules.WeightHighRisk + "%"],
            ["Scope breadth", GovernanceScoring.ScopeText(i.Scope, s.SuggestedScope), rules.WeightScope + "%"],
        ]);
        ui.KeyValues([("Granted", Fmt.Indian(s.GrantedCount)), ("Used", Fmt.Indian(s.UsedCount)), ("Unused", Fmt.Indian(s.GrantedCount - s.UsedCount))]);

        ui.Section($"Permissions Used ({s.UsedCount} of {Fmt.Indian(s.GrantedCount)})");
        if (usage.Count == 0) ui.WriteLine("No permission usage stored for this identity in this analysis.");
        else
            ui.Table(["Action", "First Seen", "Last Seen", "Events", "Resource Group"],
                usage.Select(u => (IReadOnlyList<string>)[u.Action, Fmt.Date(u.FirstUsed), Fmt.Date(u.LastUsed), u.EventCount.ToString(), u.ResourceGroup ?? "-"]).ToList(), 3);

        ui.Section($"Unused High-Risk Permissions ({unused.Count})");
        if (unused.Count == 0) ui.WriteLine("None.");
        foreach (var f in unused) ui.WriteLine($"  ! {f.Action}  ({f.Category}, {f.Severity}) - Never used");

        ui.WriteLine();
        ui.WriteLine(role is null ? "  1. View generated role (none for this identity)" : $"  1. View generated role ({role.RoleName})");
        ui.WriteLine("  2. Export evidence (JSON file)");
        ui.WriteLine("  0. Back");
        switch (ui.Ask("Choose: "))
        {
            case "1":
                if (role is null) throw new UserFacingException("No role was generated for this identity (it used no permission, or it uses all of them).");
                await RoleDetailAsync(session, me, role.GeneratedRoleId);
                break;
            case "2":
                var json = JsonSerializer.Serialize(new
                {
                    identity = new { i.UserName, i.DisplayName, i.IdentityType, i.AssignedRole, i.Scope, i.ScopeName },
                    score = s.RiskScore, riskLevel = s.RiskLevel, granted = s.GrantedCount, used = s.UsedCount,
                    permissionsUsed = usage.Select(u => new { u.Action, first = Fmt.Date(u.FirstUsed), last = Fmt.Date(u.LastUsed), events = u.EventCount, u.ResourceGroup }),
                    unusedHighRisk = unused.Select(f => f.Action), exportedAt = clock.GetUtcNow().ToString("o"),
                }, new JsonSerializerOptions { WriteIndented = true });
                ui.Ok("Saved " + await GovernanceService.SaveExportAsync(null, $"{i.UserName}-evidence.json", json));
                break;
        }
    }

    /* ------------------------------------------------------------------ findings */

    private async Task FindingsAsync(Session session, User me)
    {
        var analysis = await PickAnalysisAsync(null) ?? throw new UserFacingException("No analysis yet. Run the Analysis Wizard first.");
        string? category = null, severity = null;
        int? identityId = null;
        var page = 1;
        while (true)
        {
            var all = await gov.FindingsAsync(analysis.AnalysisId);
            var open = all.Where(f => f.Status == "Open").ToList();
            var rows = all.Where(f => (category is null || f.Category == category) && (identityId is null || f.IdentityId == identityId) && (severity is null || f.Severity == severity)).ToList();
            var p = Paged<Finding>.FromList(rows, page, PageSize);
            page = p.Page;
            ui.Section($"High-Risk Findings - {analysis.Code} · {analysis.Company.CompanyName}");
            ui.WriteLine($"Total Findings {open.Count} · " + string.Join(" · ", GovernanceService.Categories.Select(c => $"{(c == "Delete" ? "Delete Actions" : c == "Network" ? "Network (NSG)" : c)} {open.Count(f => f.Category == c)}")));
            ui.WriteLine($"Category: {category ?? "All"} · Identity: {all.FirstOrDefault(f => f.IdentityId == identityId)?.Identity.DisplayName ?? "All"} · Severity: {severity ?? "All"}");
            ui.Table(["#", "ID", "Identity", "Unused High-Risk Action", "Category", "Last Used", "Current Role", "Severity"],
                p.Items.Select((f, i) => (IReadOnlyList<string>)[(i + 1).ToString(), f.Code ?? "", Fmt.Cut(f.Identity.DisplayName, 26), f.Action, f.Category,
                    f.LastUsed is null ? "Never" : Fmt.Date(f.LastUsed.Value), Fmt.Cut(f.GrantedByRole, 20), f.Severity]).ToList(), 0);
            ui.WriteLine(ListNav.Footer(p, "findings"));
            ui.WriteLine("Type a row number to view the identity, or: K category · I identity · V severity · A other analysis · E export CSV · C clear · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, p.Pages)) continue;
            var row = ListNav.RowIndex(answer, p.Items.Count);
            if (row >= 0)
            {
                await runner.SafeRunAsync(() => IdentityDetailAsync(session, me, analysis.AnalysisId, p.Items[row].IdentityId));
                if (session.Ended) return;
                continue;
            }
            switch (answer.ToUpperInvariant())
            {
                case "K": var c = ui.AskChoice("Category", ["All", .. GovernanceService.Categories]); category = c == "All" ? null : c; page = 1; break;
                case "I":
                    var people = all.Select(f => f.Identity).DistinctBy(x => x.IdentityId).OrderBy(x => x.DisplayName).ToList();
                    var who = ui.AskChoice("Identity", ["All", .. people.Select(x => $"{x.DisplayName} ({x.UserName})")]);
                    identityId = who == "All" ? null : people.First(x => $"{x.DisplayName} ({x.UserName})" == who).IdentityId;
                    page = 1;
                    break;
                case "V": var v = ui.AskChoice("Severity", ["All", "HIGH", "MEDIUM", "LOW"]); severity = v == "All" ? null : v; page = 1; break;
                case "A": analysis = await PickAnalysisAsync(analysis.AnalysisId) ?? analysis; category = severity = null; identityId = null; page = 1; break;
                case "E":
                    var file = await GovernanceService.SaveExportAsync(null, "high-risk-findings.csv", GovernanceService.FindingsCsv(rows));
                    ui.Ok($"Saved {rows.Count} findings to {file}");
                    break;
                case "C": category = severity = null; identityId = null; page = 1; break;
                default: ui.Error("Unknown choice. Type a row number, K, I, V, A, E, C, N, P or 0."); break;
            }
        }
    }

    /* ------------------------------------------------------------------ generated roles */

    private async Task RolesAsync(Session session, User me)
    {
        var analysis = await PickAnalysisAsync(null) ?? throw new UserFacingException("No analysis yet. Run the Analysis Wizard first.");
        var page = 1;
        while (true)
        {
            var roles = await gov.RolesAsync(analysis.AnalysisId);
            var p = Paged<GeneratedRole>.FromList(roles, page, PageSize);
            page = p.Page;
            ui.Section($"Generated Roles - {analysis.Code} · {analysis.Company.CompanyName} · {roles.Count(r => r.IsReviewed)} of {roles.Count} reviewed");
            ui.Table(["#", "Role", "Identity", "Original Role", "Suggested Scope", "Actions", "Status"],
                p.Items.Select((r, i) => (IReadOnlyList<string>)[(i + 1).ToString(), r.RoleName, Fmt.Cut(r.Identity.DisplayName, 26), r.OriginalRole, r.AssignableScope,
                    GovernanceService.RoleActions(r).Count.ToString(), r.IsReviewed ? "Reviewed" : "Pending"]).ToList(), 0, 5);
            ui.WriteLine(ListNav.Footer(p, "roles"));
            ui.WriteLine("Type a row number to open a role, or: A other analysis · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, p.Pages)) continue;
            var row = ListNav.RowIndex(answer, p.Items.Count);
            if (row >= 0)
            {
                await runner.SafeRunAsync(() => RoleDetailAsync(session, me, p.Items[row].GeneratedRoleId));
                if (session.Ended) return;
            }
            else if (answer.Equals("A", StringComparison.OrdinalIgnoreCase)) { analysis = await PickAnalysisAsync(analysis.AnalysisId) ?? analysis; page = 1; }
            else ui.Error("Unknown choice. Type a row number, A, N, P or 0.");
        }
    }

    private async Task RoleDetailAsync(Session session, User me, int roleId)
    {
        var rules = await gov.ActiveRulesAsync();
        await runner.RunAsync(session, "Generated Role", async m =>
        {
            var r = await gov.RoleAsync(roleId) ?? throw new UserFacingException("Role not found.");
            var rare = r.GeneratedRoleActions.Where(a => a.IsRare).ToList();
            ui.KeyValues([
                ("Role", r.RoleName), ("Status", r.IsReviewed ? $"Reviewed by {r.ReviewedByNavigation?.FullName} on {Fmt.DateTime(r.ReviewedAt)}" : "Pending review"),
                ("Identity", $"{r.Identity.DisplayName} ({r.Identity.UserName})"), ("Original Role", r.OriginalRole),
                ("Suggested Scope", r.AssignableScope + " (Resource Group)"), ("Actions", GovernanceService.RoleActions(r).Count.ToString()),
                ("Baseline read actions", r.IncludeBaselineRead ? "Included" : "Not included"),
            ]);
            if (rare.Count > 0)
            {
                ui.WriteLine($"  Rare / Review Actions ({rare.Count}) - used fewer than {rules.RareThreshold} times, confirm before keeping them in the role:");
                foreach (var a in rare) ui.WriteLine($"    - {a.Action} - {a.Note}");
            }
            else ui.WriteLine("  No rare actions: every action in this role was used regularly.");
            ui.WriteLine("  Role definition (JSON):");
            foreach (var line in GovernanceService.RoleJson(r).Split('\n')) ui.WriteLine("    " + line.TrimEnd('\r'));
            ui.WriteLine($"  Azure CLI: az role definition create --role-definition {r.RoleName}.json");
            return (IReadOnlyList<MenuItem>)
            [
                new(r.IsReviewed ? "Mark as Pending" : "Mark as Reviewed", async u =>
                {
                    await gov.SetReviewedAsync(roleId, !r.IsReviewed, u.UserId);
                    ui.Ok(r.IsReviewed ? $"{r.RoleName} moved back to pending" : $"{r.RoleName} marked as reviewed");
                }),
                new(r.IncludeBaselineRead ? "Exclude baseline read actions" : "Include baseline read actions", async u =>
                {
                    await gov.SetBaselineReadAsync(roleId, !r.IncludeBaselineRead, u.UserId);
                    ui.Ok("Saved.");
                }),
                new("Save role JSON file", async u => ui.Ok("Saved " + await GovernanceService.SaveExportAsync(null, r.RoleName + ".json", GovernanceService.RoleJson(r)))),
            ];
        }, topLevel: false);
    }

    /* ------------------------------------------------------------------ rules + patterns */

    private async Task RulesAsync(User me)
    {
        while (true)
        {
            var r = await gov.ActiveRulesAsync();
            var total = r.WeightUnused + r.WeightHighRisk + r.WeightScope;
            ui.Section("Rule Settings (rules.json)");
            ui.Table(["Signal", "Weight"],
            [
                ["Unused permission ratio", r.WeightUnused + "%"], ["Unused high-risk actions", r.WeightHighRisk + "%"], ["Scope breadth", r.WeightScope + "%"],
                ["Total", $"{total}% ({(total == 100 ? "Valid" : "Must be 100%")})"],
            ], 1);
            ui.KeyValues([
                ("Default analysis window", r.DefaultPeriodDays + " days"), ("Rare action threshold", $"used fewer than {r.RareThreshold} times"),
                ("Include baseline read actions", r.IncludeBaselineRead ? "Yes" : "No"),
                ("Last updated", r.UpdatedByNavigation is null ? "-" : $"{Fmt.DateTime(r.UpdatedAt)} by {r.UpdatedByNavigation.FullName}"),
            ]);
            ui.WriteLine("  1. Edit weights and settings");
            ui.WriteLine("  2. View rules.json");
            ui.WriteLine("  3. Reset to default");
            ui.WriteLine("  0. Back");
            switch (ui.Ask("Choose: "))
            {
                case "0": return;
                case "1":
                    await runner.SafeRunAsync(async () =>
                    {
                        ui.WriteLine("Weights are percentages and must add up to 100. Enter keeps the value in [brackets]. Type 'cancel' to go back.");
                        var wu = ui.AskInt("Unused permission ratio weight", 0, 100, "Whole number from 0 to 100", r.WeightUnused);
                        var wh = ui.AskInt("Unused high-risk actions weight", 0, 100, "Whole number from 0 to 100", r.WeightHighRisk);
                        var ws = ui.AskInt("Scope breadth weight", 0, 100, "Whole number from 0 to 100", r.WeightScope);
                        ui.WriteLine($"Total: {wu + wh + ws}%");
                        var window = ui.AskChoice("Default analysis window", GovernanceService.PeriodPresets.Select(p => p + " days").ToList(),
                            GovernanceService.PeriodPresets.Contains(r.DefaultPeriodDays) ? r.DefaultPeriodDays + " days" : "90 days");
                        var rare = ui.AskInt("Rare action threshold", 1, 50, "Rare action threshold must be 1 to 50", r.RareThreshold);
                        var baseline = ui.AskChoice("Include baseline read actions", ["Yes", "No"], r.IncludeBaselineRead ? "Yes" : "No") == "Yes";
                        await gov.SaveRulesAsync(wu, wh, ws, int.Parse(window.Split(' ')[0]), rare, baseline, me.UserId);
                        ui.Ok("rules.json saved");
                    });
                    break;
                case "2":
                    ui.Section("rules.json (current values)");
                    ui.WriteLine(GovernanceService.RulesJson(r, await gov.PatternsAsync()));
                    break;
                case "3":
                    if (ui.AskYesNo("Reset to default? All weights, patterns and settings will go back to the default values"))
                    {
                        await runner.SafeRunAsync(() => gov.ResetRulesAsync(me.UserId));
                        ui.Ok("Rules reset to default");
                    }
                    break;
                default: ui.Error("Type 1, 2, 3 or 0."); break;
            }
        }
    }

    private async Task PatternsAsync(User me)
    {
        while (true)
        {
            var list = await gov.PatternsAsync();
            ui.Section("High-Risk Patterns (an unused granted action that matches an enabled pattern is a finding)");
            if (list.Count == 0) ui.WriteLine("No patterns. Add one.");
            else ui.Table(["#", "Pattern", "Enabled"], list.Select((p, i) => (IReadOnlyList<string>)[(i + 1).ToString(), p.Pattern, p.IsEnabled ? "Yes" : "No"]).ToList(), 0);
            ui.WriteLine("Type a row number to enable/disable or delete a pattern, or: A add pattern · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            var row = ListNav.RowIndex(answer, list.Count);
            if (answer.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                await runner.SafeRunAsync(async () =>
                {
                    ui.WriteLine("Use * as a wildcard. Example: Microsoft.Compute/*/delete  (type 'cancel' to go back)");
                    var pattern = ui.AskValid("Pattern", v => GovernanceService.CheckPattern(v, list));
                    await gov.AddPatternAsync(pattern, me.UserId);
                    ui.Ok("Pattern added");
                });
            }
            else if (row >= 0)
            {
                var p = list[row];
                ui.WriteLine($"  1. {(p.IsEnabled ? "Disable" : "Enable")} {p.Pattern}");
                ui.WriteLine($"  2. Delete {p.Pattern}");
                ui.WriteLine("  0. Back");
                var c = ui.Ask("Choose: ");
                if (c == "1") await runner.SafeRunAsync(async () => { await gov.SetPatternEnabledAsync(p.PatternId, !p.IsEnabled, me.UserId); ui.Ok(p.IsEnabled ? "Pattern disabled" : "Pattern enabled"); });
                if (c == "2" && ui.AskYesNo($"Delete pattern? Remove {p.Pattern}"))
                    await runner.SafeRunAsync(async () => { await gov.DeletePatternAsync(p.PatternId, me.UserId); ui.Ok("Pattern removed"); });
            }
            else ui.Error("Unknown choice. Type a row number, A or 0.");
        }
    }

    /* ------------------------------------------------------------------ reports */

    private async Task ReportsAsync(User me)
    {
        while (true)
        {
            var reports = await gov.ReportsAsync();
            ui.Section("Reports");
            ui.Table(["#", "Report ID", "Company", "Date", "Identities", "Avg Score", "High-Risk"],
                reports.Select((r, i) => (IReadOnlyList<string>)[(i + 1).ToString(), r.Code, r.Analysis.Company.CompanyName, Fmt.Date(DateOnly.FromDateTime(r.GeneratedAt)),
                    r.IdentityCount.ToString(), Fmt.Dec2(r.AvgScore), r.HighRiskCount.ToString()]).ToList(), 0, 4, 5, 6);
            ui.WriteLine("Type a row number to preview a report, or: G generate new report · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            var row = ListNav.RowIndex(answer, reports.Count);
            if (answer.Equals("G", StringComparison.OrdinalIgnoreCase))
            {
                await runner.SafeRunAsync(async () =>
                {
                    var companies = await gov.CompaniesAsync();
                    ui.WriteLine("The report uses the latest analysis for the selected company.");
                    var name = ui.AskChoice("Company", companies.Select(c => c.CompanyName).ToList());
                    var rep = await gov.GenerateReportAsync(companies.First(c => c.CompanyName == name).CompanyId, me.UserId);
                    ui.Ok($"{rep.Code} generated for {name}");
                });
            }
            else if (row >= 0) await runner.SafeRunAsync(() => ReportPreviewAsync(reports[row]));
            else ui.Error("Unknown choice. Type a row number, G or 0.");
        }
    }

    private async Task ReportPreviewAsync(Report r)
    {
        var (fix, high, medium, low) = await gov.ReportDetailAsync(r.AnalysisId);
        ui.Section($"Report Preview - {r.Code}");
        ui.WriteLine($"Cloud Governance Report · {r.Code} · {r.Analysis.Company.CompanyName} · {Fmt.Date(DateOnly.FromDateTime(r.GeneratedAt))} · analysis {r.Analysis.Code}");
        ui.KeyValues([("Identities", r.IdentityCount.ToString()), ("Avg score", Fmt.Dec2(r.AvgScore)), ("High-risk unused", r.HighRiskCount.ToString())]);
        ui.WriteLine("Fix First:");
        for (var i = 0; i < fix.Count; i++) ui.WriteLine($"  {i + 1}. {fix[i].DisplayName} ({fix[i].UserName}) - score {fix[i].RiskScore} ({fix[i].AssignedRole})");
        ui.WriteLine("Score by risk band:");
        var max = Math.Max(high, Math.Max(medium, low));
        ui.Table(["Band", "Identities", ""], [["High (70-100)", high.ToString(), ConsoleUi.Bar(high, max)], ["Medium (40-69)", medium.ToString(), ConsoleUi.Bar(medium, max)], ["Low (0-39)", low.ToString(), ConsoleUi.Bar(low, max)]], 1);
        if (ui.AskYesNo("Save the full report as an HTML file (open it in a browser to print)?"))
            ui.Ok("Saved " + await GovernanceService.SaveExportAsync(null, r.Code + ".html", await gov.ReportHtmlAsync(r)));
    }

    /* ------------------------------------------------------------------ notifications */

    private async Task NotificationsAsync(User me)
    {
        var list = await gov.NotificationsAsync(me.UserId);
        ui.Section("Notifications");
        if (list.Count == 0)
        {
            ui.WriteLine("No notifications.");
            return;
        }
        foreach (var n in list) ui.WriteLine($"  {(n.IsRead ? " " : "*")} {n.Message}   ({Fmt.DateTime(n.CreatedAt)})");
        ui.WriteLine("  (* = unread)");
        if (list.Any(n => !n.IsRead) && ui.AskYesNo("Mark all read?"))
        {
            var count = await gov.MarkAllReadAsync(me.UserId);
            ui.Ok($"All notifications marked as read ({count}).");
        }
    }
}
