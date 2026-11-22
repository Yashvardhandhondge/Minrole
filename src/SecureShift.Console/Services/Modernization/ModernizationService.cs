using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Context.Generated;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Services.Modernization;

/// <summary>The classification answers of the form (classification.js) before they are saved. Not an entity: raw typed text.</summary>
public sealed record ClassificationForm(string TotalApps, string Deps, string Apis, string Loc, string Dbs, string StorageGb, string Envs, string Cloud,
    string Units, string Users, IReadOnlyList<string> Techs, IReadOnlyList<string> Policies, string CritLow, string CritMedium, string CritHigh, string CritCritical);

/// <summary>
/// Admin (EMDT): enterprises, EMSI classification, application inventory, components, dependency links,
/// digital twin data (modernization/js/admin-dashboard.js, enterprise.js, classification.js, applications.js,
/// register-application.js, dependencies.js, digital-twin.js). Every write runs in a transaction with its audit row.
/// </summary>
public sealed partial class ModernizationService(IDbContextFactory<SecureShiftDbContext> factory, TimeProvider clock) : ServiceBase(factory, clock)
{
    public static readonly string[] Industries = ["Banking", "Insurance", "Retail", "Healthcare", "Manufacturing", "Telecom", "Government", "Other"];
    public static readonly string[] AppStatuses = ["Active", "Legacy"];
    public static readonly string[] Deployments = ["On-Premise", "Private Cloud", "Public Cloud", "Hybrid Cloud"];
    public static readonly string[] Supports = ["Supported", "Extended", "Unsupported"];
    public static readonly string[] DocQualities = ["Good", "Average", "Poor"];
    public static readonly string[] LowMediumHigh = ["Low", "Medium", "High"];
    public static readonly string[] ComponentTypes = ["Service", "Database", "API"];

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 &.\-]{2,99}$")] private static partial Regex EnterpriseNameRx();
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 &.\-]*$")] private static partial Regex AppTextRx();
    [GeneratedRegex(@"^[A-Za-z.#+ ]{2,}[0-9]+(\.[0-9]+)*$")] private static partial Regex FrameworkRx();
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 ._\-]{2,59}$")] private static partial Regex ComponentNameRx();

    /* ------------------------------------------------------------------ enterprises */

    public async Task<List<Enterprise>> EnterprisesAsync()
    {
        await using var db = await NewDbAsync();
        return await db.Enterprises.AsNoTracking()
            .Include(e => e.Classification).ThenInclude(c => c!.ClassificationTechnologies)
            .Include(e => e.Classification).ThenInclude(c => c!.ClassificationPolicies)
            .Include(e => e.Applications).Include(e => e.Components).Include(e => e.Dependencies)
            .AsSplitQuery().OrderBy(e => e.EnterpriseId).ToListAsync();
    }

    public async Task<Enterprise?> EnterpriseAsync(int id) => (await EnterprisesAsync()).FirstOrDefault(e => e.EnterpriseId == id);

    /// <summary>The EMSI result of an enterprise (null = not classified yet).</summary>
    public static EmsiResult? EmsiOf(Enterprise e) =>
        e.Classification is null ? null
            : EmdtCalc.Emsi(e.Classification, e.Classification.ClassificationTechnologies.Select(t => t.Technology).ToList(),
                e.Classification.ClassificationPolicies.Select(p => p.PolicyName).ToList());

    public static string EnterpriseName(string v, IEnumerable<Enterprise> all, int? exceptId)
    {
        if (!EnterpriseNameRx().IsMatch(v)) return "3-100 characters: letters, numbers, space, & . -";
        return all.Any(e => e.EnterpriseId != exceptId && e.Name.Equals(v, StringComparison.OrdinalIgnoreCase)) ? "This enterprise is already registered" : "";
    }

    public static string LettersName(string v) => Validation.IsName(v) ? "" : "Letters only, 3-50 characters";
    public static string EnterpriseEmail(string v) => Validation.IsEmail(v) ? "" : "Enter a valid email";
    public static string EnterprisePhone(string v) => Validation.IsPhone(v) ? "" : "10 digits starting with 6, 7, 8 or 9";

    /// <summary>"Register New Enterprise" (admin-dashboard.js). Step 1 of onboarding.</summary>
    public async Task<Enterprise> RegisterEnterpriseAsync(string name, string industry, string city, string contact, string email, string phone, int actorId)
    {
        (name, city, contact, email, phone) = (name.Trim(), city.Trim(), contact.Trim(), email.Trim(), phone.Trim());
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var all = await db.Enterprises.AsNoTracking().ToListAsync();
        Validation.ThrowIfAny(
            ("Enterprise Name", EnterpriseName(name, all, null)),
            ("Industry", Industries.Contains(industry) ? "" : "Select an industry"),
            ("Headquarters City", LettersName(city)),
            ("Contact Person", LettersName(contact)),
            ("Contact Email", EnterpriseEmail(email)),
            ("Contact Phone", EnterprisePhone(phone)));
        var e = new Enterprise { Name = name, Industry = industry, City = city, ContactName = contact, ContactEmail = email, ContactPhone = phone, RegisteredBy = actorId, RegisteredAt = Today };
        db.Enterprises.Add(e);
        await db.SaveChangesAsync();
        Audit.Add(db, actorId, "Enterprise Registered", e.Name, $"ENT-{e.EnterpriseId:000}", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        e.Code = $"ENT-{e.EnterpriseId:000}";
        return e;
    }

    /// <summary>"Edit Registration" (enterprise.js): city, contact, email, phone.</summary>
    public async Task UpdateRegistrationAsync(int enterpriseId, string city, string contact, string email, string phone, int actorId)
    {
        (city, contact, email, phone) = (city.Trim(), contact.Trim(), email.Trim(), phone.Trim());
        Validation.ThrowIfAny(
            ("Headquarters City", LettersName(city)), ("Contact Person", LettersName(contact)),
            ("Contact Email", EnterpriseEmail(email)), ("Contact Phone", Validation.IsPhone(phone) ? "" : "10 digits starting with 6-9"));
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var e = await db.Enterprises.FirstOrDefaultAsync(x => x.EnterpriseId == enterpriseId) ?? throw new UserFacingException("Enterprise not found.");
        (e.City, e.ContactName, e.ContactEmail, e.ContactPhone) = (city, contact, email, phone);
        Audit.Add(db, actorId, "Enterprise Updated", e.Name, e.Code, UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /* ------------------------------------------------------------------ classification (EMSI) */

    /// <summary>
    /// classification.js rules() with the exact messages. Where the DB is stricter than the prototype the DB
    /// limit is used (Total applications 1-10,000). Returns (field, message) pairs; empty = valid.
    /// </summary>
    public static List<(string Field, string Error)> CheckClassification(ClassificationForm f)
    {
        var e = new List<(string, string)>();
        void Rule(string field, bool ok, string message) { if (!ok) e.Add((field, message)); }
        Rule("Total applications", Validation.IsInt(f.TotalApps, 1, 10000), "Whole number from 1 to 10,000");
        Rule("Number of dependencies", Validation.IsInt(f.Deps, 0, 1000000), "Whole number, 0 or more");
        Rule("Number of APIs", Validation.IsInt(f.Apis, 0, 100000), "Whole number, 0 or more");
        Rule("Lines of code", Validation.IsLong(f.Loc, 1000, 1000000000), "Whole number from 1,000 to 1,000,000,000");
        Rule("Databases", Validation.IsInt(f.Dbs, 1, 100000), "At least 1 database");
        Rule("Storage (GB)", Validation.IsInt(f.StorageGb, 1, 10000000), "Whole number of GB, at least 1");
        Rule("Environments", Validation.IsInt(f.Envs, 1, 20), "Whole number from 1 to 20");
        Rule("Cloud type", EmdtCalc.CloudTypes.Contains(f.Cloud), "Select a cloud type");
        Rule("Business units", Validation.IsInt(f.Units, 1, 500), "Whole number from 1 to 500");
        Rule("Active users", Validation.IsInt(f.Users, 1, 10000000), "Whole number, at least 1");
        Rule("Technologies", f.Techs.Count > 0, "Select at least one technology");
        foreach (var (name, v) in new[] { ("Low", f.CritLow), ("Medium", f.CritMedium), ("High", f.CritHigh), ("Critical", f.CritCritical) })
            Rule($"Criticality {name}", Validation.IsInt(v, 0, 100000), "0 or more");
        if (e.Count == 0)
        {
            var sum = int.Parse(f.CritLow) + int.Parse(f.CritMedium) + int.Parse(f.CritHigh) + int.Parse(f.CritCritical);
            Rule("Criticality Critical", sum == int.Parse(f.TotalApps), "Low + Medium + High + Critical must equal Total applications");
            Rule("Number of APIs", !(long.Parse(f.Apis) > long.Parse(f.Deps) * 2 + 50), "APIs look too high compared with dependencies - please check");
        }
        return e;
    }

    /// <summary>"Save &amp; Calculate EMSI": stores the answers + EMSI score (the DB adds the category).</summary>
    public async Task<(Classification Saved, EmsiResult Result)> ClassifyAsync(int enterpriseId, ClassificationForm f, int actorId)
    {
        var errors = CheckClassification(f);
        if (errors.Count > 0) throw new UserFacingException("EMSI not calculated - please fix these fields:" + Environment.NewLine + "        " +
                                                            string.Join(Environment.NewLine + "        ", errors.Select(x => $"{x.Field}: {x.Error}")));
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ent = await db.Enterprises.FirstOrDefaultAsync(e => e.EnterpriseId == enterpriseId) ?? throw new UserFacingException("Enterprise not found.");
        var c = await db.Classifications.Include(x => x.ClassificationTechnologies).Include(x => x.ClassificationPolicies).FirstOrDefaultAsync(x => x.EnterpriseId == enterpriseId);
        if (c is null)
        {
            c = new Classification { EnterpriseId = enterpriseId };
            db.Classifications.Add(c);
        }
        c.TotalApps = int.Parse(f.TotalApps);
        c.Dependencies = int.Parse(f.Deps);
        c.Apis = int.Parse(f.Apis);
        c.DatabaseCount = int.Parse(f.Dbs);
        c.StorageGb = int.Parse(f.StorageGb);
        c.Environments = int.Parse(f.Envs);
        c.BusinessUnits = int.Parse(f.Units);
        c.UserCount = int.Parse(f.Users);
        c.CritLow = int.Parse(f.CritLow);
        c.CritMedium = int.Parse(f.CritMedium);
        c.CritHigh = int.Parse(f.CritHigh);
        c.CritCritical = int.Parse(f.CritCritical);
        c.CloudType = EmdtCalc.CloudToDb(f.Cloud);
        c.LinesOfCode = long.Parse(f.Loc);
        c.ClassifiedBy = actorId;
        c.ClassifiedAt = Today;
        c.ClassificationTechnologies.Clear();
        foreach (var t in EmdtCalc.Technologies.Where(f.Techs.Contains)) c.ClassificationTechnologies.Add(new ClassificationTechnology { Technology = t });
        c.ClassificationPolicies.Clear();
        foreach (var p in EmdtCalc.Policies.Where(f.Policies.Contains)) c.ClassificationPolicies.Add(new ClassificationPolicy { PolicyName = p });

        var result = EmdtCalc.Emsi(c, f.Techs, f.Policies);
        c.EmsiScore = (decimal)result.Score;
        Audit.Add(db, actorId, "Enterprise Classified", ent.Name, $"EMSI {Fmt.Dec2(result.Score)} · {result.Category.Name}", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (c, result);
    }

    /* ------------------------------------------------------------------ applications */

    /// <summary>All the dependency links of an enterprise with their source / target loaded (for DI and the twin).</summary>
    public async Task<List<Dependency>> LinksAsync(int enterpriseId)
    {
        await using var db = await NewDbAsync();
        return await db.Dependencies.AsNoTracking().Where(d => d.EnterpriseId == enterpriseId)
            .Include(d => d.SourceApplication).Include(d => d.SourceComponent).Include(d => d.TargetApplication).Include(d => d.TargetComponent)
            .OrderBy(d => d.DependencyId).ToListAsync();
    }

    public async Task<List<Application>> ApplicationsAsync(int enterpriseId)
    {
        await using var db = await NewDbAsync();
        return await db.Applications.AsNoTracking().Include(a => a.Components).Include(a => a.Assessments)
            .Where(a => a.EnterpriseId == enterpriseId).OrderBy(a => a.ApplicationId).ToListAsync();
    }

    public async Task<Application?> ApplicationAsync(int applicationId)
    {
        await using var db = await NewDbAsync();
        return await db.Applications.AsNoTracking().Include(a => a.Enterprise).Include(a => a.Components)
            .Include(a => a.Assessments).ThenInclude(x => x.AssessedByNavigation).FirstOrDefaultAsync(a => a.ApplicationId == applicationId);
    }

    public static string AppText(string v, int min, int max) =>
        v.Length >= min && v.Length <= max && AppTextRx().IsMatch(v) ? "" : $"{min}-{max} characters: letters, numbers, space & . -";

    public static string Framework(string v) => v.Length <= 50 && FrameworkRx().IsMatch(v) ? "" : "Name + version number, e.g. Java 8 or .NET 6";

    /// <summary>register-application.js appRules(); the DB allows an age of 0-50 years (prototype 0-60).</summary>
    public static List<(string Field, string Error)> CheckApplication(Application a, IEnumerable<Application> sameEnterprise, string enterpriseName)
    {
        var e = new List<(string, string)>();
        void Rule(string field, string error) { if (error.Length > 0) e.Add((field, error)); }
        var nameError = AppText(a.Name, 3, 60);
        if (nameError.Length == 0 && sameEnterprise.Any(x => x.ApplicationId != a.ApplicationId && x.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase)))
            nameError = "This application already exists in " + enterpriseName;
        Rule("Application Name", nameError);
        Rule("Business Unit", AppText(a.BusinessUnit ?? "", 2, 50));
        Rule("Owner Team", AppText(a.OwnerTeam, 2, 50));
        Rule("Status", AppStatuses.Contains(a.Status) || a.Status == "Retired" ? "" : "Select a status");
        Rule("Deployment", Deployments.Contains(a.Deployment) ? "" : "Select a deployment type");
        Rule("Technology", EmdtCalc.Technologies.Contains(a.Technology) ? "" : "Select a technology");
        Rule("Framework", Framework(a.Framework));
        Rule("Framework support", Supports.Contains(a.SupportStatus) ? "" : "Choose one");
        Rule("Age (years)", a.AgeYears is >= 0 and <= 50 ? "" : "Whole number from 0 to 50");
        Rule("Documentation quality", DocQualities.Contains(a.DocQuality) ? "" : "Choose one");
        Rule("Code complexity", LowMediumHigh.Contains(a.Complexity) ? "" : "Choose one");
        Rule("Lines of code", a.LinesOfCode is >= 100 and <= 1000000000 ? "" : "Whole number, at least 100");
        Rule("Business criticality", EmdtCalc.Criticalities.Contains(a.Criticality) ? "" : "Choose one");
        Rule("Security issues", a.SecurityIssues is >= 0 and <= 999 ? "" : "Whole number from 0 to 999");
        Rule("Downtime tolerance", LowMediumHigh.Contains(a.DowntimeTolerance) ? "" : "Choose one");
        Rule("Compliance policies", a.CompliancePolicies is >= 0 and <= 10 ? "" : "Whole number from 0 to 10");
        return e;
    }

    /// <summary>Register (ApplicationId = 0) or update an application. Audit "Application Registered" / "Application Updated".</summary>
    public async Task<Application> SaveApplicationAsync(Application input, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ent = await db.Enterprises.FirstOrDefaultAsync(x => x.EnterpriseId == input.EnterpriseId) ?? throw new UserFacingException("Enterprise not found.");
        var same = await db.Applications.AsNoTracking().Where(a => a.EnterpriseId == input.EnterpriseId).ToListAsync();
        var errors = CheckApplication(input, same, ent.Name);
        if (errors.Count > 0) throw new UserFacingException("Please fix the highlighted fields:" + Environment.NewLine + "        " + string.Join(Environment.NewLine + "        ", errors.Select(x => $"{x.Field}: {x.Error}")));

        Application app;
        if (input.ApplicationId == 0)
        {
            app = input;
            app.CreatedAt = UtcNow;
            db.Applications.Add(app);
            await db.SaveChangesAsync();
            Audit.Add(db, actorId, "Application Registered", app.Name, $"APP-{app.ApplicationId:000} · {ent.Name}", UtcNow);
        }
        else
        {
            app = await db.Applications.FirstOrDefaultAsync(a => a.ApplicationId == input.ApplicationId && a.EnterpriseId == input.EnterpriseId) ?? throw new UserFacingException("Application not found.");
            (app.Name, app.BusinessUnit, app.OwnerTeam, app.Status, app.Deployment, app.Technology, app.Framework, app.SupportStatus, app.AgeYears) =
                (input.Name, input.BusinessUnit, input.OwnerTeam, input.Status, input.Deployment, input.Technology, input.Framework, input.SupportStatus, input.AgeYears);
            (app.DocQuality, app.Complexity, app.LinesOfCode, app.Criticality, app.HasSensitiveData, app.SecurityIssues, app.DowntimeTolerance, app.CompliancePolicies) =
                (input.DocQuality, input.Complexity, input.LinesOfCode, input.Criticality, input.HasSensitiveData, input.SecurityIssues, input.DowntimeTolerance, input.CompliancePolicies);
            Audit.Add(db, actorId, "Application Updated", app.Name, app.Code, UtcNow);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        app.Code ??= $"APP-{app.ApplicationId:000}";
        return app;
    }

    /// <summary>
    /// "Delete application?" (applications.js): its components and every link that uses it are removed too.
    /// An application that already has assessments / simulations cannot be deleted (the DB keeps that history).
    /// </summary>
    public async Task<int> DeleteApplicationAsync(int applicationId, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var app = await db.Applications.Include(a => a.Components).FirstOrDefaultAsync(a => a.ApplicationId == applicationId) ?? throw new UserFacingException("Application not found.");
        if (await db.Assessments.AnyAsync(x => x.ApplicationId == applicationId) || await db.Simulations.AnyAsync(x => x.ApplicationId == applicationId))
            throw new UserFacingException($"{app.Name} has assessments or simulations, so it cannot be deleted (the history must stay).");
        var compIds = app.Components.Select(c => c.ComponentId).ToList();
        var links = await db.Dependencies.Where(d => d.SourceApplicationId == applicationId || d.TargetApplicationId == applicationId
                                                     || (d.SourceComponentId != null && compIds.Contains(d.SourceComponentId.Value))
                                                     || (d.TargetComponentId != null && compIds.Contains(d.TargetComponentId.Value))).ToListAsync();
        db.Dependencies.RemoveRange(links);
        db.Components.RemoveRange(app.Components);
        db.Applications.Remove(app);
        Audit.Add(db, actorId, "Application Deleted", app.Name, app.Code, UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return links.Count;
    }

    /* ------------------------------------------------------------------ components */

    public async Task<List<Component>> ComponentsAsync(int enterpriseId)
    {
        await using var db = await NewDbAsync();
        return await db.Components.AsNoTracking().Include(c => c.Application).Where(c => c.EnterpriseId == enterpriseId).OrderBy(c => c.ComponentId).ToListAsync();
    }

    public static string ComponentName(string v, IEnumerable<Component> sameEnterprise)
    {
        if (!ComponentNameRx().IsMatch(v)) return "3-60 characters: letters, numbers, space . _ -";
        return sameEnterprise.Any(c => c.Name.Equals(v, StringComparison.OrdinalIgnoreCase)) ? "A component with this name already exists" : "";
    }

    /// <summary>"Add Component" (dependencies.js): Service / Database / API of one application.</summary>
    public async Task<Component> AddComponentAsync(int enterpriseId, string type, int? applicationId, string name, int actorId)
    {
        name = name.Trim();
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var app = applicationId is null ? null : await db.Applications.FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.EnterpriseId == enterpriseId);
        Validation.ThrowIfAny(
            ("Type", ComponentTypes.Contains(type) ? "" : "Select a type"),
            ("Parent Application", app is null ? "Select the parent application" : ""),
            ("Component Name", ComponentName(name, await db.Components.AsNoTracking().Where(c => c.EnterpriseId == enterpriseId).ToListAsync())));
        var c = new Component { EnterpriseId = enterpriseId, ApplicationId = app!.ApplicationId, ComponentType = type, Name = name };
        db.Components.Add(c);
        Audit.Add(db, actorId, "Component Added", name, $"{type} · {app.Name}", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        c.Code = $"N-{c.ComponentId:00}";
        return c;
    }

    public async Task DeleteComponentAsync(int componentId, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await db.Components.FirstOrDefaultAsync(x => x.ComponentId == componentId) ?? throw new UserFacingException("Component not found.");
        var used = await db.Dependencies.CountAsync(d => d.SourceComponentId == componentId || d.TargetComponentId == componentId);
        if (used > 0) throw new UserFacingException($"Remove the {used} link(s) using this component first");
        db.Components.Remove(c);
        Audit.Add(db, actorId, "Component Deleted", c.Name, c.ComponentType, UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /* ------------------------------------------------------------------ dependencies */

    /// <summary>A link end: an application or a component ("A12" / "C5" in the menus).</summary>
    public sealed record Node(int? ApplicationId, int? ComponentId, string Name, string Type, int? OwnerApplicationId);

    public async Task<List<Node>> NodesAsync(int enterpriseId)
    {
        var apps = await ApplicationsAsync(enterpriseId);
        var comps = await ComponentsAsync(enterpriseId);
        var nodes = new List<Node>();
        foreach (var a in apps)
        {
            nodes.Add(new Node(a.ApplicationId, null, a.Name, "Application", a.ApplicationId));
            nodes.AddRange(comps.Where(c => c.ApplicationId == a.ApplicationId).Select(c => new Node(null, c.ComponentId, c.Name, c.ComponentType, c.ApplicationId)));
        }
        nodes.AddRange(comps.Where(c => c.ApplicationId is null).Select(c => new Node(null, c.ComponentId, c.Name, c.ComponentType, null)));
        return nodes;
    }

    /// <summary>dependencies.js formRules2(): required, not the same, a component not on its own parent app, no duplicates.</summary>
    public static List<(string Field, string Error)> CheckLink(Node? source, Node? target, string? criticality, string? description, IEnumerable<Dependency> links, int? exceptId)
    {
        var e = new List<(string, string)>();
        if (source is null) e.Add(("Source", "Source is required"));
        if (target is null) e.Add(("Target", "Target is required"));
        else if (source is not null)
        {
            if (source.ApplicationId == target.ApplicationId && source.ComponentId == target.ComponentId) e.Add(("Target", "Source and target cannot be the same"));
            else if (source.ComponentId is not null && target.ApplicationId is not null && source.OwnerApplicationId == target.ApplicationId)
                e.Add(("Target", "A component cannot depend on its own parent application"));
            else if (links.Any(l => l.DependencyId != exceptId && l.SourceApplicationId == source.ApplicationId && l.SourceComponentId == source.ComponentId
                                    && l.TargetApplicationId == target.ApplicationId && l.TargetComponentId == target.ComponentId))
                e.Add(("Target", "This link already exists"));
        }
        if (criticality is null || !EmdtCalc.Criticalities.Contains(criticality)) e.Add(("Criticality", "Criticality is required"));
        if ((description ?? "").Length > 200) e.Add(("Description", "Max 200 characters"));
        return e;
    }

    /// <summary>"Save Dependency" (new when dependencyId is null) / "Update Dependency".</summary>
    public async Task<Dependency> SaveLinkAsync(int enterpriseId, int? dependencyId, Node source, Node target, string criticality, string? description, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var links = await db.Dependencies.AsNoTracking().Where(d => d.EnterpriseId == enterpriseId).ToListAsync();
        var errors = CheckLink(source, target, criticality, description, links, dependencyId);
        if (errors.Count > 0) throw new UserFacingException(string.Join("; ", errors.Select(x => $"{x.Field}: {x.Error}")));

        Dependency d;
        if (dependencyId is null)
        {
            d = new Dependency { EnterpriseId = enterpriseId, CreatedBy = actorId };
            db.Dependencies.Add(d);
        }
        else d = await db.Dependencies.FirstOrDefaultAsync(x => x.DependencyId == dependencyId && x.EnterpriseId == enterpriseId) ?? throw new UserFacingException("Dependency not found.");
        (d.SourceApplicationId, d.SourceComponentId, d.TargetApplicationId, d.TargetComponentId) = (source.ApplicationId, source.ComponentId, target.ApplicationId, target.ComponentId);
        d.Criticality = criticality;
        d.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        await db.SaveChangesAsync();
        if (dependencyId is null) Audit.Add(db, actorId, "Dependency Added", $"{source.Name} → {target.Name}", target.Type, UtcNow);
        else Audit.Add(db, actorId, "Dependency Updated", $"DEP-{d.DependencyId:000}", "", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        d.Code = $"DEP-{d.DependencyId:000}";
        return d;
    }

    public async Task DeleteLinkAsync(int dependencyId, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var d = await db.Dependencies.FirstOrDefaultAsync(x => x.DependencyId == dependencyId) ?? throw new UserFacingException("Dependency not found.");
        db.Dependencies.Remove(d);
        Audit.Add(db, actorId, "Dependency Deleted", d.Code, "", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
