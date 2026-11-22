using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Modernization;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// Admin (EMDT): enterprises (dashboard, register, details, edit), EMSI classification, application inventory,
/// components and dependency links, digital twin (modernization/js/admin-dashboard.js ... digital-twin.js).
/// The Admin works inside one "current enterprise" at a time, like the prototype's enterprise picker.
/// </summary>
public sealed class AdminMenu(ConsoleUi ui, MenuRunner runner, ProfileMenu profile, ModernizationService emdt, ModernizationScreens screens)
{
    private const int PageSize = 10;

    public Task RunAsync(Session session) =>
        runner.RunAsync(session, "Admin · Application Modernization", async me =>
        {
            var ent = (await emdt.EnterprisesAsync()).FirstOrDefault(e => e.EnterpriseId == session.EnterpriseId) ?? (await emdt.EnterprisesAsync()).FirstOrDefault();
            if (ent is not null) session.EnterpriseId = ent.EnterpriseId;
            ui.WriteLine($"Current enterprise: {(ent is null ? "(none registered yet)" : $"{ent.Name} ({ent.Code})")}");
            return (IReadOnlyList<MenuItem>)
            [
                new("Dashboard (all enterprises)", m => DashboardAsync(session, m)),
                new("Register New Enterprise", m => RegisterEnterpriseAsync(session, m)),
                new("Choose current enterprise", _ => screens.ChooseEnterpriseAsync(session)),
                new("Enterprise Details (registration, EMSI, pricing)", m => EnterpriseDetailsAsync(session, m)),
                new("EMSI Classification", m => ClassifyAsync(session, m)),
                new("Application Inventory", m => ApplicationsAsync(session, m)),
                new("Components & Dependencies", m => DependenciesAsync(session, m)),
                new("Digital Twin", _ => screens.DigitalTwinAsync(session)),
                .. profile.CommonItems(session),
            ];
        }, topLevel: true);

    /* ------------------------------------------------------------------ dashboard */

    private async Task DashboardAsync(Session session, User me)
    {
        string? search = null, cat = null;
        var page = 1;
        while (true)
        {
            var ents = await emdt.EnterprisesAsync();
            var rows = ents.Select(e => (E: e, R: ModernizationService.EmsiOf(e))).ToList();
            ui.Section("Admin Dashboard - Enterprises");
            ui.KeyValues([
                ("Total Enterprises", ents.Count.ToString()), ("Classified", ents.Count(e => e.Classification is not null).ToString()),
                ("Pending Classification", ents.Count(e => e.Classification is null).ToString()), ("Total Applications", ents.Sum(e => e.Applications.Count).ToString()),
            ]);
            var cats = new[] { "Foundation", "Growth", "Enterprise", "Strategic", "Not classified" }
                .Select(c => (c, rows.Count(r => (r.R?.Category.Name ?? "Not classified") == c))).Where(x => x.Item2 > 0).ToList();
            ui.WriteLine("Category: " + string.Join(" · ", cats.Select(c => $"{c.c} {c.Item2}")));

            var list = rows.Where(r => search is null || $"{r.E.Name} {r.E.Code} {r.E.City}".Contains(search, StringComparison.OrdinalIgnoreCase))
                .Where(r => cat is null || (cat == "Pending" ? r.R is null : r.R?.Category.Name == cat)).ToList();
            var p = Paged<(Enterprise E, EmsiResult? R)>.FromList(list, page, PageSize);
            page = p.Page;
            ui.WriteLine($"Search: {search ?? "(none)"} · Category: {cat ?? "All"} · {list.Count} of {ents.Count}");
            ui.Table(["#", "ID", "Enterprise", "City", "Industry", "Apps", "EMSI", "Category", "Plan / Month"],
                p.Items.Select((r, i) => (IReadOnlyList<string>)[(i + 1).ToString(), r.E.Code ?? "", r.E.Name, r.E.City ?? "", r.E.Industry, r.E.Applications.Count.ToString(),
                    r.R is null ? "-" : Fmt.Dec2(r.R.Score), r.R?.Category.Name ?? "Pending", r.R is null ? "-" : Fmt.Rupees(r.R.Category.Monthly)]).ToList(), 0, 5, 6);
            ui.WriteLine(ListNav.Footer(p, "enterprises"));
            ui.WriteLine("Type a row number to open an enterprise, or: R register · S search · K category · C clear · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, p.Pages)) continue;
            var row = ListNav.RowIndex(answer, p.Items.Count);
            if (row >= 0)
            {
                session.EnterpriseId = p.Items[row].E.EnterpriseId;
                await runner.SafeRunAsync(() => EnterpriseDetailsAsync(session, me));
                if (session.Ended) return;
                continue;
            }
            switch (answer.ToUpperInvariant())
            {
                case "R": await runner.SafeRunAsync(() => RegisterEnterpriseAsync(session, me)); break;
                case "S": var s = ui.Ask("Search name, ID or city (Enter = no search): "); search = s.Length == 0 ? null : s; page = 1; break;
                case "K": var k = ui.AskChoice("Category", ["All", "Foundation", "Growth", "Enterprise", "Strategic", "Pending"]); cat = k == "All" ? null : k; page = 1; break;
                case "C": search = cat = null; page = 1; break;
                default: ui.Error("Unknown choice. Type a row number, R, S, K, C, N, P or 0."); break;
            }
        }
    }

    /* ------------------------------------------------------------------ enterprise */

    private async Task RegisterEnterpriseAsync(Session session, User me)
    {
        ui.Section("Register New Enterprise");
        ui.WriteLine("Step 1 of onboarding. After registering, fill the classification to calculate the EMSI and category. Type 'cancel' to go back.");
        var all = await emdt.EnterprisesAsync();
        var name = ui.AskValid("Enterprise Name", v => ModernizationService.EnterpriseName(v, all, null));
        var industry = ui.AskChoice("Industry", ModernizationService.Industries, requiredMessage: "Select an industry");
        var city = ui.AskValid("Headquarters City", ModernizationService.LettersName);
        var contact = ui.AskValid("Contact Person", ModernizationService.LettersName);
        var email = ui.AskValid("Contact Email", ModernizationService.EnterpriseEmail);
        var phone = ui.AskValid("Contact Phone", ModernizationService.EnterprisePhone);
        var e = await emdt.RegisterEnterpriseAsync(name, industry, city, contact, email, phone, me.UserId);
        session.EnterpriseId = e.EnterpriseId;
        ui.Ok($"{e.Name} registered as {e.Code}. Next: classification (menu 5 - EMSI Classification).");
    }

    private async Task EnterpriseDetailsAsync(Session session, User me)
    {
        var e = await screens.CurrentAsync(session);
        var r = ModernizationService.EmsiOf(e);
        ui.Section($"{e.Name} ({e.Code}) - {(r is null ? "Pending Classification" : "Classified")}");
        ui.KeyValues([
            ("EMSI Score", r is null ? "-" : Fmt.Dec2(r.Score)), ("Category", r?.Category.Name ?? "Pending"), ("Applications", e.Applications.Count.ToString()),
            ("Dependency Links", $"{e.Dependencies.Count} ({e.Components.Count} components)"),
        ]);
        ui.WriteLine("Registration:");
        ui.KeyValues([
            ("Enterprise ID", e.Code ?? ""), ("Name", e.Name), ("Industry", e.Industry), ("Headquarters", e.City ?? "-"), ("Contact Person", e.ContactName),
            ("Email", e.ContactEmail), ("Phone", e.ContactPhone ?? "-"), ("Registered On", Fmt.Date(e.RegisteredAt)),
            ("Classified On", e.Classification is null ? "-" : Fmt.Date(e.Classification.ClassifiedAt)),
        ]);
        if (r is null) ui.WriteLine("Not classified yet. Choose 2 to add the portfolio details: EMSI, category and pricing are calculated automatically.");
        else ShowEmsi(r);
        ui.WriteLine("  1. Edit registration");
        ui.WriteLine($"  2. {(r is null ? "Add" : "Edit")} classification details");
        ui.WriteLine("  0. Back");
        switch (ui.Ask("Choose: "))
        {
            case "1":
                ui.Section("Edit Registration");
                ui.WriteLine("Press Enter to keep the value in [brackets]. Type 'cancel' to go back.");
                var city = ui.AskValid("Headquarters City", ModernizationService.LettersName, e.City ?? "");
                var contact = ui.AskValid("Contact Person", ModernizationService.LettersName, e.ContactName);
                var email = ui.AskValid("Contact Email", ModernizationService.EnterpriseEmail, e.ContactEmail);
                var phone = ui.AskValid("Contact Phone", v => Validation.IsPhone(v) ? "" : "10 digits starting with 6-9", e.ContactPhone?.Trim() ?? "");
                await emdt.UpdateRegistrationAsync(e.EnterpriseId, city, contact, email, phone, me.UserId);
                ui.Ok("Registration details saved");
                break;
            case "2":
                await ClassifyAsync(session, me);
                break;
        }
    }

    /// <summary>EMSI ring + category scale + plan (enterprise.js / classification.js showResult).</summary>
    private void ShowEmsi(EmsiResult r)
    {
        ui.WriteLine($"EMSI {Fmt.Dec2(r.Score)} / 100 · Category: {r.Category.Name}");
        ui.WriteLine("Scale: " + string.Join("  ", EmdtCalc.Categories.Select(c => (c.Name == r.Category.Name ? "[" : "") + $"{c.Name} {c.Min}-{Math.Ceiling(c.Max)}" + (c.Name == r.Category.Name ? "]" : ""))));
        ui.WriteLine($"{r.Category.Name} Subscription Plan: Monthly {Fmt.Rupees(r.Category.Monthly)} · Annual (-10%) {Fmt.Rupees(r.Category.Annual)}");
        ui.Table(["Parameter", "Your Input", "Score", "Weight", "Weighted"],
            r.Rows.Select(x => (IReadOnlyList<string>)[x.Label, x.Input, Fmt.Js(x.Score), (x.Weight * 100).ToString("0") + "%", Fmt.Js(x.Weighted)]).ToList(), 2, 3, 4);
    }

    /* ------------------------------------------------------------------ classification */

    private async Task ClassifyAsync(Session session, User me)
    {
        var e = await screens.CurrentAsync(session);
        var c = e.Classification;
        ui.Section($"Portfolio Details - {e.Name}");
        ui.WriteLine("Enter the 12 parameters. Press Enter to keep the value in [brackets]. Type 'cancel' to go back.");
        string? S(int? v) => v?.ToString();
        var techs = c?.ClassificationTechnologies.Select(t => t.Technology).ToList();
        var pols = c?.ClassificationPolicies.Select(p => p.PolicyName).ToList();
        ClassificationForm f;
        while (true)
        {
            var total = ui.AskValid("Total applications", v => Validation.IsInt(v, 1, 10000) ? "" : "Whole number from 1 to 10,000", S(c?.TotalApps));
            var deps = ui.AskValid("Number of dependencies", v => Validation.IsInt(v, 0, 1000000) ? "" : "Whole number, 0 or more", S(c?.Dependencies));
            var apis = ui.AskValid("Number of APIs", v => Validation.IsInt(v, 0, 100000) ? "" : "Whole number, 0 or more", S(c?.Apis));
            var loc = ui.AskValid("Lines of code", v => Validation.IsLong(v, 1000, 1000000000) ? "" : "Whole number from 1,000 to 1,000,000,000", c?.LinesOfCode.ToString());
            var dbs = ui.AskValid("Databases", v => Validation.IsInt(v, 1, 100000) ? "" : "At least 1 database", S(c?.DatabaseCount));
            var storage = ui.AskValid("Storage (GB)", v => Validation.IsInt(v, 1, 10000000) ? "" : "Whole number of GB, at least 1", S(c?.StorageGb));
            var envs = ui.AskValid("Environments", v => Validation.IsInt(v, 1, 20) ? "" : "Whole number from 1 to 20", S(c?.Environments));
            var cloud = ui.AskChoice("Cloud infrastructure type", EmdtCalc.CloudTypes, c is null ? null : EmdtCalc.CloudFromDb(c.CloudType), "Select a cloud type");
            var units = ui.AskValid("Business units", v => Validation.IsInt(v, 1, 500) ? "" : "Whole number from 1 to 500", S(c?.BusinessUnits));
            var users = ui.AskValid("Active users", v => Validation.IsInt(v, 1, 10000000) ? "" : "Whole number, at least 1", S(c?.UserCount));
            techs = ui.AskMulti("Technologies (value: Java 75, PHP 95, .NET 120, COBOL 450)", EmdtCalc.Technologies, x => x.Count > 0 ? "" : "Select at least one technology", techs);
            pols = ui.AskMulti("Governance policies", EmdtCalc.Policies, _ => "", pols ?? []);
            ui.WriteLine("Application criticality (Low + Medium + High + Critical must equal Total applications):");
            var low = ui.AskValid("  Low", v => Validation.IsInt(v, 0, 100000) ? "" : "0 or more", S(c?.CritLow));
            var med = ui.AskValid("  Medium", v => Validation.IsInt(v, 0, 100000) ? "" : "0 or more", S(c?.CritMedium));
            var high = ui.AskValid("  High", v => Validation.IsInt(v, 0, 100000) ? "" : "0 or more", S(c?.CritHigh));
            var crit = ui.AskValid("  Critical", v => Validation.IsInt(v, 0, 100000) ? "" : "0 or more", S(c?.CritCritical));
            f = new ClassificationForm(total, deps, apis, loc, dbs, storage, envs, cloud, units, users, techs, pols, low, med, high, crit);
            var sum = int.Parse(low) + int.Parse(med) + int.Parse(high) + int.Parse(crit);
            ui.WriteLine($"Sum of levels: {sum} / Total applications: {total} {(sum == int.Parse(total) ? "(Matches)" : "(Must match)")}");
            var errors = ModernizationService.CheckClassification(f);
            if (errors.Count == 0) break;
            ui.Error("EMSI not calculated - please fix the highlighted fields:");
            foreach (var (field, error) in errors) ui.WriteLine($"        {field}: {error}");
            if (!ui.AskYesNo("Fix the answers now? (your answers are kept as the defaults)")) return;
            c = new Classification
            {
                TotalApps = int.Parse(total), Dependencies = int.Parse(deps), Apis = int.Parse(apis), LinesOfCode = long.Parse(loc), DatabaseCount = int.Parse(dbs),
                StorageGb = int.Parse(storage), Environments = int.Parse(envs), CloudType = EmdtCalc.CloudToDb(cloud), BusinessUnits = int.Parse(units), UserCount = int.Parse(users),
                CritLow = int.Parse(low), CritMedium = int.Parse(med), CritHigh = int.Parse(high), CritCritical = int.Parse(crit),
            };
        }
        var (_, result) = await emdt.ClassifyAsync(e.EnterpriseId, f, me.UserId);
        ui.Ok($"EMSI {Fmt.Dec2(result.Score)} - {result.Category.Name} category");
        ShowEmsi(result);
    }

    /* ------------------------------------------------------------------ applications */

    private async Task ApplicationsAsync(Session session, User me)
    {
        string? search = null, tech = null, crit = null, assessed = null;
        var page = 1;
        while (true)
        {
            var ent = await screens.CurrentAsync(session);
            var apps = await emdt.ApplicationsAsync(ent.EnterpriseId);
            var links = await emdt.LinksAsync(ent.EnterpriseId);
            var r = ModernizationService.EmsiOf(ent);
            ui.Section($"Application Inventory - {ent.Name} ({ent.Code})");
            ui.WriteLine(r is null ? $"{ent.Name} is not classified yet. Add classification details first (menu 5)."
                : $"EMSI {Fmt.Dec2(r.Score)} · {r.Category.Name} · Subscription {r.Category.Name} {Fmt.Rupees(r.Category.Monthly)}/month · Portfolio (declared) {ent.Classification!.TotalApps} apps");
            ui.KeyValues([
                ("Total Applications", apps.Count.ToString()), ("Critical Applications", apps.Count(a => a.Criticality == "Critical").ToString()),
                ("Legacy Applications", apps.Count(a => a.Status == "Legacy").ToString()), ("Dependency Links", links.Count.ToString()),
            ]);
            var rows = apps.Where(a => search is null || $"{a.Name} {a.Code}".Contains(search, StringComparison.OrdinalIgnoreCase))
                .Where(a => (tech is null || a.Technology == tech) && (crit is null || a.Criticality == crit))
                .Where(a => assessed is null || (assessed == "Assessed") == (a.Assessments.Count > 0)).ToList();
            var p = Paged<Application>.FromList(rows, page, PageSize);
            page = p.Page;
            ui.WriteLine($"Search: {search ?? "(none)"} · Technology: {tech ?? "All"} · Criticality: {crit ?? "All"} · Assessment: {assessed ?? "All"}");
            ui.Table(["#", "ID", "Application", "Owner", "Technology", "Framework · Support", "Criticality", "Deployment", "Age", "Links", "Status"],
                p.Items.Select((a, i) => (IReadOnlyList<string>)[(i + 1).ToString(), a.Code ?? "", a.Name, a.OwnerTeam, a.Technology, $"{a.Framework} · {a.SupportStatus}",
                    a.Criticality, a.Deployment, a.AgeYears + " yrs", EmdtCalc.AppLinks(a.ApplicationId, links).Count.ToString(), a.Status]).ToList(), 0, 8, 9);
            ui.WriteLine(ListNav.Footer(p, "applications"));
            ui.WriteLine("Type a row number to view / edit / delete, or: R register · S search · T technology · K criticality · A assessment · C clear · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, p.Pages)) continue;
            var row = ListNav.RowIndex(answer, p.Items.Count);
            if (row >= 0)
            {
                await runner.SafeRunAsync(() => ApplicationActionsAsync(me, p.Items[row].ApplicationId));
                continue;
            }
            switch (answer.ToUpperInvariant())
            {
                case "R": await runner.SafeRunAsync(() => ApplicationFormAsync(me, ent, null)); break;
                case "S": var s = ui.Ask("Search name or ID (Enter = no search): "); search = s.Length == 0 ? null : s; page = 1; break;
                case "T": var t = ui.AskChoice("Technology", ["All", .. EmdtCalc.Technologies]); tech = t == "All" ? null : t; page = 1; break;
                case "K": var k = ui.AskChoice("Criticality", ["All", .. EmdtCalc.Criticalities]); crit = k == "All" ? null : k; page = 1; break;
                case "A": var a2 = ui.AskChoice("Assessment", ["All", "Assessed", "Not Assessed"]); assessed = a2 == "All" ? null : a2; page = 1; break;
                case "C": search = tech = crit = assessed = null; page = 1; break;
                default: ui.Error("Unknown choice. Type a row number, R, S, T, K, A, C, N, P or 0."); break;
            }
        }
    }

    private async Task ApplicationActionsAsync(User me, int applicationId)
    {
        await screens.ShowApplicationAsync(applicationId);
        ui.WriteLine("  1. Edit application");
        ui.WriteLine("  2. Delete application");
        ui.WriteLine("  0. Back");
        switch (ui.Ask("Choose: "))
        {
            case "1":
                var app = await emdt.ApplicationAsync(applicationId) ?? throw new UserFacingException("Application not found.");
                await ApplicationFormAsync(me, app.Enterprise, app);
                break;
            case "2":
                var a = await emdt.ApplicationAsync(applicationId) ?? throw new UserFacingException("Application not found.");
                var links = (await emdt.LinksAsync(a.EnterpriseId)).Count(l => EmdtCalc.SourceOwner(l) == a.ApplicationId || EmdtCalc.TargetOwner(l) == a.ApplicationId);
                ui.WriteLine(links > 0 ? $"Its {links} dependency link(s) and components will also be removed." : "This cannot be undone.");
                if (!ui.AskYesNo($"Delete application? Delete {a.Name} from the inventory")) return;
                await emdt.DeleteApplicationAsync(applicationId, me.UserId);
                ui.Ok($"{a.Name} deleted");
                break;
        }
    }

    /// <summary>register-application.js: register (app = null) or edit an application.</summary>
    private async Task ApplicationFormAsync(User me, Enterprise ent, Application? app)
    {
        var all = await emdt.ApplicationsAsync(ent.EnterpriseId);
        ui.Section(app is null ? $"Register Application - {ent.Name}" : $"Edit Application - {app.Code}");
        ui.WriteLine(app is null ? "Type 'cancel' at any question to go back." : "Update application information to keep the inventory accurate. Enter keeps the value in [brackets].");
        var a = new Application { ApplicationId = app?.ApplicationId ?? 0, EnterpriseId = ent.EnterpriseId };
        a.Name = ui.AskValid("Application Name", v =>
        {
            var err = ModernizationService.AppText(v, 3, 60);
            return err.Length > 0 ? err : all.Any(x => x.ApplicationId != a.ApplicationId && x.Name.Equals(v, StringComparison.OrdinalIgnoreCase)) ? "This application already exists in " + ent.Name : "";
        }, app?.Name);
        a.BusinessUnit = ui.AskValid("Business Unit", v => ModernizationService.AppText(v, 2, 50), app?.BusinessUnit);
        a.OwnerTeam = ui.AskValid("Owner Team", v => ModernizationService.AppText(v, 2, 50), app?.OwnerTeam);
        a.Status = ui.AskChoice("Status", ModernizationService.AppStatuses, app?.Status, "Select a status");
        a.Deployment = ui.AskChoice("Deployment", ModernizationService.Deployments, app?.Deployment, "Select a deployment type");
        a.Technology = ui.AskChoice("Technology", EmdtCalc.Technologies, app?.Technology, "Select a technology");
        a.Framework = ui.AskValid("Framework + version (e.g. Java 8 or .NET 6)", ModernizationService.Framework, app?.Framework);
        a.SupportStatus = ui.AskChoice("Framework support", ModernizationService.Supports, app?.SupportStatus, "Choose one");
        a.AgeYears = ui.AskInt("Age (years)", 0, 50, "Whole number from 0 to 50", app?.AgeYears);
        a.DocQuality = ui.AskChoice("Documentation quality", ModernizationService.DocQualities, app?.DocQuality, "Choose one");
        a.Complexity = ui.AskChoice("Code complexity", ModernizationService.LowMediumHigh, app?.Complexity, "Choose one");
        a.LinesOfCode = ui.AskLong("Lines of code", 100, 1000000000, "Whole number, at least 100", app?.LinesOfCode);
        a.Criticality = ui.AskChoice("Business criticality", EmdtCalc.Criticalities, app?.Criticality, "Choose one");
        a.HasSensitiveData = ui.AskChoice("Sensitive data", ["Yes", "No"], app is null ? null : ModernizationScreens.Yes(app.HasSensitiveData), "Choose one") == "Yes";
        a.SecurityIssues = ui.AskInt("Known security issues", 0, 999, "Whole number from 0 to 999", app?.SecurityIssues);
        a.DowntimeTolerance = ui.AskChoice("Downtime tolerance", ModernizationService.LowMediumHigh, app?.DowntimeTolerance, "Choose one");
        a.CompliancePolicies = ui.AskInt("Compliance policies that apply", 0, 10, "Whole number from 0 to 10", app?.CompliancePolicies);

        if (app is not null)
        {
            ui.WriteLine("Existing assessments keep their old scores. Re-assess the application to use the new values.");
            if (!ui.AskYesNo($"Confirm Update: update {app.Name}?")) { ui.Info("Cancelled. Nothing was saved."); return; }
        }
        var saved = await emdt.SaveApplicationAsync(a, me.UserId);
        ui.Ok(app is null ? $"{saved.Name} registered as {saved.Code}. Next: add its components and dependencies (menu 7)." : $"{saved.Name} updated");
    }

    /* ------------------------------------------------------------------ components + dependencies */

    private async Task DependenciesAsync(Session session, User me)
    {
        string? search = null, type = null, crit = null;
        while (true)
        {
            var ent = await screens.CurrentAsync(session);
            var apps = await emdt.ApplicationsAsync(ent.EnterpriseId);
            var comps = await emdt.ComponentsAsync(ent.EnterpriseId);
            var links = await emdt.LinksAsync(ent.EnterpriseId);
            ui.Section($"Components & Dependencies - {ent.Name} ({ent.Code})");
            ui.KeyValues([
                ("Applications", apps.Count.ToString()),
                ("Components", $"{comps.Count} ({comps.Count(c => c.ComponentType == "Service")} svc / {comps.Count(c => c.ComponentType == "Database")} db / {comps.Count(c => c.ComponentType == "API")} api)"),
                ("Dependency Links", links.Count.ToString()), ("Critical Links", links.Count(l => l.Criticality == "Critical").ToString()),
            ]);
            ui.WriteLine("Components:");
            ui.Table(["#", "ID", "Component", "Type", "Application"],
                comps.Select((c, i) => (IReadOnlyList<string>)[(i + 1).ToString(), c.Code ?? "", c.Name, c.ComponentType, c.Application?.Name ?? "-"]).ToList(), 0);
            var shown = links.Where(l => search is null || $"{EmdtCalc.SourceName(l)} {EmdtCalc.TargetName(l)} {l.Description} {l.Code}".Contains(search, StringComparison.OrdinalIgnoreCase))
                .Where(l => (type is null || EmdtCalc.LinkType(l) == type) && (crit is null || l.Criticality == crit)).ToList();
            ui.WriteLine($"Dependency links (source depends on target) · Search: {search ?? "(none)"} · Type: {type ?? "All"} · Criticality: {crit ?? "All"}:");
            ui.Table(["#", "ID", "Source", "Target", "Type", "Criticality", "Description"],
                shown.Select((l, i) => (IReadOnlyList<string>)[(i + 1).ToString(), l.Code ?? "", EmdtCalc.SourceName(l), EmdtCalc.TargetName(l), EmdtCalc.LinkType(l), l.Criticality, Fmt.Cut(l.Description, 30)]).ToList(), 0);
            ui.WriteLine("A add component · X delete component · L add link · E edit link · D delete link · I impact results · S search · F filter · C clear · 0 back");
            var answer = ui.Ask("Choose: ").ToUpperInvariant();
            switch (answer)
            {
                case "0": return;
                case "A": await runner.SafeRunAsync(() => AddComponentAsync(me, ent, apps, comps)); break;
                case "X":
                    await runner.SafeRunAsync(async () =>
                    {
                        var n = ui.AskInt("Component number (#)", 1, Math.Max(1, comps.Count), "Type a number from the # column of Components");
                        if (comps.Count == 0) throw new UserFacingException("There are no components.");
                        var c = comps[n - 1];
                        if (!ui.AskYesNo($"Delete component? Delete {c.Name}")) return;
                        await emdt.DeleteComponentAsync(c.ComponentId, me.UserId);
                        ui.Ok("Component deleted");
                    });
                    break;
                case "L": await runner.SafeRunAsync(() => LinkFormAsync(me, ent, apps, links, null)); break;
                case "E" or "D":
                    await runner.SafeRunAsync(async () =>
                    {
                        if (shown.Count == 0) throw new UserFacingException("There are no links in the list.");
                        var n = ui.AskInt("Link number (#)", 1, shown.Count, "Type a number from the # column of the links");
                        var l = shown[n - 1];
                        if (answer == "E") { await LinkFormAsync(me, ent, apps, links, l); return; }
                        if (!ui.AskYesNo($"Delete dependency? Remove link {l.Code}")) return;
                        await emdt.DeleteLinkAsync(l.DependencyId, me.UserId);
                        ui.Ok("Dependency deleted");
                    });
                    break;
                case "I":
                    ui.Section("Dependency Impact (outgoing links of each application)");
                    if (apps.Count == 0) ui.WriteLine("Register applications first.");
                    var res = apps.Select(a => (a, di: EmdtCalc.DepImpact(a.ApplicationId, links))).OrderByDescending(x => x.di.Raw).ToList();
                    ui.Table(["Application", "ID", "Outgoing Links", "Raw Points", "Dependency Impact", "Level"],
                        res.Select(x => (IReadOnlyList<string>)[x.a.Name, x.a.Code ?? "", x.di.Count.ToString(), x.di.Raw.ToString(), $"{x.di.Score} {ConsoleUi.Bar(x.di.Score, 100, 10)}", EmdtCalc.Level(x.di.Score)]).ToList(), 2, 3);
                    ui.WriteLine("Dependency Impact = 100 x (1 - e^(-raw / 20)); raw = sum of type weight x criticality points.");
                    break;
                case "S": var s = ui.Ask("Search source, target, description or ID (Enter = no search): "); search = s.Length == 0 ? null : s; break;
                case "F":
                    var t = ui.AskChoice("Type (of the target)", ["All", "Application", .. ModernizationService.ComponentTypes]); type = t == "All" ? null : t;
                    var k = ui.AskChoice("Criticality", ["All", .. EmdtCalc.Criticalities]); crit = k == "All" ? null : k;
                    break;
                case "C": search = type = crit = null; break;
                default: ui.Error("Unknown choice. Type A, X, L, E, D, I, S, F, C or 0."); break;
            }
        }
    }

    private async Task AddComponentAsync(User me, Enterprise ent, List<Application> apps, List<Component> comps)
    {
        if (apps.Count == 0) throw new UserFacingException("Register an application first (Application Inventory)");
        ui.Section("Add Component");
        ui.WriteLine("Type 'cancel' at any question to go back.");
        var type = ui.AskChoice("Type", ModernizationService.ComponentTypes, requiredMessage: "Select a type");
        var labels = apps.Select(a => $"{a.Name} ({a.Code})").ToList();
        var app = apps[labels.IndexOf(ui.AskChoice("Parent Application", labels, requiredMessage: "Select the parent application"))];
        var name = ui.AskValid("Component Name", v => ModernizationService.ComponentName(v, comps));
        var c = await emdt.AddComponentAsync(ent.EnterpriseId, type, app.ApplicationId, name, me.UserId);
        ui.Ok($"{type} {c.Name} added ({c.Code})");
    }

    /// <summary>"Add Dependency" / "Edit Dependency" (dependencies.js linkForm).</summary>
    private async Task LinkFormAsync(User me, Enterprise ent, List<Application> apps, List<Dependency> links, Dependency? edit)
    {
        if (apps.Count == 0) throw new UserFacingException("Register an application first (Application Inventory)");
        var nodes = await emdt.NodesAsync(ent.EnterpriseId);
        var labels = nodes.Select(n => n.Type == "Application" ? $"{n.Name} (Application)" : $"   {n.Name} ({n.Type} of {apps.FirstOrDefault(a => a.ApplicationId == n.OwnerApplicationId)?.Name ?? "no application"})").ToList();
        ModernizationService.Node? Find(int? appId, int? compId) => nodes.FirstOrDefault(n => n.ApplicationId == appId && n.ComponentId == compId);
        string? Label(ModernizationService.Node? n) => n is null ? null : labels[nodes.IndexOf(n)];

        ui.Section(edit is null ? "Add Dependency" : $"Edit Dependency {edit.Code}");
        ui.WriteLine("The Source depends on the Target. The dependency type is the type of the target. Type 'cancel' to go back.");
        while (true)
        {
            var source = nodes[labels.IndexOf(ui.AskChoice("Source (depends on something)", labels, Label(edit is null ? null : Find(edit.SourceApplicationId, edit.SourceComponentId)), "Source is required"))];
            var target = nodes[labels.IndexOf(ui.AskChoice("Target (is depended upon)", labels, Label(edit is null ? null : Find(edit.TargetApplicationId, edit.TargetComponentId)), "Target is required"))];
            ui.WriteLine($"Dependency Type: {target.Type}");
            var crit = ui.AskChoice("Criticality", EmdtCalc.Criticalities, edit?.Criticality, "Criticality is required");
            var desc = ui.AskValid("Description (optional, max 200)", v => v.Length <= 200 ? "" : "Max 200 characters", edit?.Description ?? "");
            var errors = ModernizationService.CheckLink(source, target, crit, desc, links, edit?.DependencyId);
            if (errors.Count == 0)
            {
                var d = await emdt.SaveLinkAsync(ent.EnterpriseId, edit?.DependencyId, source, target, crit, desc, me.UserId);
                ui.Ok(edit is null ? $"Dependency {d.Code} saved" : $"Dependency {d.Code} updated");
                return;
            }
            foreach (var (field, error) in errors) ui.Error($"{field}: {error}");
            if (!ui.AskYesNo("Try again?")) return;
        }
    }
}
