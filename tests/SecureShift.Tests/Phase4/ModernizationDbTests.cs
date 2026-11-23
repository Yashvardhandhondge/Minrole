using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Modernization;
using SecureShift.Data.Models.Generated;
using SecureShift.Data.Context.Generated;

namespace SecureShift.Tests.Phase4;

/// <summary>The SEEDED applications + links give the prototype's assessment and dependency numbers (read-only, shared seeded DB).</summary>
[Collection(DatabaseCollection.Name)]
public class SeedEmdtTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Seed_apps_match_the_prototype_assessment_dependency_impact_and_affected_systems()
    {
        await using var db = fixture.CreateContext();
        var apps = await db.Applications.AsNoTracking().OrderBy(a => a.ApplicationId).ToListAsync();
        var links = await db.Dependencies.AsNoTracking().Include(d => d.SourceComponent).Include(d => d.TargetComponent).Include(d => d.TargetApplication).ToListAsync();
        var names = apps.ToDictionary(a => a.ApplicationId, a => a.Name);
        foreach (var p in PrototypeData.Emdt.Items("seedApps"))
        {
            var app = apps.Single(a => a.Code == p.Str("id"));
            var outgoing = EmdtCalc.AppLinks(app.ApplicationId, links);
            Assert.Equal(p.Int("td"), EmdtCalc.TechDebt(app).Score);
            Assert.Equal(p.Int("gr"), EmdtCalc.GovRisk(app, outgoing).Score);
            var di = EmdtCalc.DepImpact(app.ApplicationId, links);
            Assert.Equal((p.Int("links"), p.Int("raw"), p.Int("di")), (di.Count, di.Raw, di.Score));
            Assert.Equal(p.Items("affected").Select(x => (x.Str("name"), x.Str("type"), x.Str("criticality"), x.Str("owner"))),
                EmdtCalc.Affected(app.ApplicationId, links, names).Select(x => (x.Name, x.Type, x.Criticality, x.Owner)));
            Assert.Equal(p.Items("tdFactors").Select(f => f[1].GetString()), EmdtCalc.TechDebt(app).Factors.Select(f => (string?)f.Value));
        }
    }

    [Fact]
    public async Task Seeded_assessments_and_simulations_equal_the_formulas()
    {
        await using var db = fixture.CreateContext();
        var sims = await db.Simulations.AsNoTracking().Include(s => s.Assessment).ToListAsync();
        foreach (var s in sims)
        {
            var r = EmdtCalc.Simulate(s.Assessment.TechnicalDebt, s.Assessment.GovernanceRisk, s.Assessment.DependencyImpact, s.Strategy, EmdtCalc.MultiplierOf(s.EmsiScore));
            Assert.Equal((s.ModernizationRisk, s.EffortWeeks, s.TeamSize, s.CostLakh, s.Recommendation, s.StrategyRisk),
                (r.ModernizationRisk, r.Effort, r.Team, (decimal)r.Cost, r.Recommendation, r.StrategyRisk));
        }
        var phases = await db.PlanPhases.AsNoTracking().Include(p => p.Plan).ThenInclude(p => p.Simulation).ToListAsync();
        foreach (var g in phases.GroupBy(p => p.PlanId))
            Assert.Equal(EmdtCalc.Phases(g.First().Plan.Simulation.EffortWeeks).Select(x => (x.No, x.Name, x.Weeks)), g.OrderBy(x => x.PhaseNo).Select(x => (x.PhaseNo, x.PhaseName, x.Weeks)));
    }
}

/// <summary>Admin writes (enterprises, classification, applications, components, links) on a fresh database.</summary>
public class ModernizationDbTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private const int Sachin = 12;   // Admin
    private ModernizationService Emdt => db.Get<ModernizationService>();

    private static ClassificationForm Form(string total = "12", string low = "3", string med = "4", string high = "3", string crit = "2", string apis = "8") =>
        new(total, "20", apis, "250000", "3", "400", "2", "Public Cloud", "2", "30", ["Java", ".NET"], ["HIPAA", "ISO 27001"], low, med, high, crit);

    private static Application App(int ent, string name = "Flight Booking", int age = 6) => new()
    {
        EnterpriseId = ent, Name = name, BusinessUnit = "Sales", OwnerTeam = "Web Team", Status = "Active", Deployment = "Public Cloud", Technology = "Java",
        Framework = "Java 17", SupportStatus = "Supported", AgeYears = age, DocQuality = "Good", Complexity = "Medium", LinesOfCode = 300000,
        Criticality = "High", HasSensitiveData = true, SecurityIssues = 1, DowntimeTolerance = "Low", CompliancePolicies = 1,
    };

    [Fact]
    public async Task Register_enterprise_with_the_prototype_rules()
    {
        var emdt = Emdt;
        var ex = await Assert.ThrowsAsync<UserFacingException>(() => emdt.RegisterEnterpriseAsync("NB", "Space", "P1", "R", "bad", "123", Sachin));
        foreach (var m in new[] { "3-100 characters: letters, numbers, space, & . -", "Select an industry", "Letters only, 3-50 characters", "Enter a valid email", "10 digits starting with 6, 7, 8 or 9" })
            Assert.Contains(m, ex.Message);
        Assert.Contains("This enterprise is already registered",
            (await Assert.ThrowsAsync<UserFacingException>(() => emdt.RegisterEnterpriseAsync("northwind bank", "Banking", "Pune", "Ravi Rao", "a@b.com", "9876543210", Sachin))).Message);

        var e = await emdt.RegisterEnterpriseAsync("Tailspin Airlines", "Other", "Pune", "Kavita Desai", "kavita@tailspin.com", "9812300011", Sachin);
        Assert.Equal("ENT-004", e.Code);
        await emdt.UpdateRegistrationAsync(e.EnterpriseId, "Mumbai", "Kavita Desai", "kavita.d@tailspin.com", "9812300012", Sachin);
        await using var ctx = db.CreateContext();
        var saved = await ctx.Enterprises.AsNoTracking().SingleAsync(x => x.EnterpriseId == e.EnterpriseId);
        Assert.Equal(("Mumbai", "kavita.d@tailspin.com", Sachin), (saved.City, saved.ContactEmail, saved.RegisteredBy));
        Assert.Equal(["Enterprise Registered", "Enterprise Updated"],
            await ctx.AuditLogs.Where(a => a.Target == "Tailspin Airlines").OrderBy(a => a.AuditLogId).Select(a => a.Action).ToListAsync());
    }

    [Fact]
    public async Task Classification_rules_and_the_saved_EMSI()
    {
        var emdt = Emdt;
        var errors = ModernizationService.CheckClassification(Form(crit: "3"));
        Assert.Contains(("Criticality Critical", "Low + Medium + High + Critical must equal Total applications"), errors);
        Assert.Contains(("Number of APIs", "APIs look too high compared with dependencies - please check"), ModernizationService.CheckClassification(Form(apis: "91")));
        Assert.Contains(("Total applications", "Whole number from 1 to 10,000"), ModernizationService.CheckClassification(Form(total: "10001")));
        Assert.Contains(("Technologies", "Select at least one technology"), ModernizationService.CheckClassification(Form() with { Techs = [] }));

        // Fabrikam Health (ENT-003) is not classified in the seed
        var (c, r) = await emdt.ClassifyAsync(3, Form(), Sachin);
        Assert.Equal(32.33, r.Score);
        await using var ctx = db.CreateContext();
        var saved = await ctx.Classifications.AsNoTracking().Include(x => x.ClassificationTechnologies).Include(x => x.ClassificationPolicies).SingleAsync(x => x.EnterpriseId == 3);
        Assert.Equal((32.33m, "Growth", "Public Cloud"), (saved.EmsiScore, saved.Category, saved.CloudType));   // Category is the DB generated column
        Assert.Equal(["Java", ".NET"], saved.ClassificationTechnologies.Select(t => t.Technology).Order(StringComparer.Ordinal).Reverse());
        Assert.Equal(2, saved.ClassificationPolicies.Count);

        // re-classify: Multi-Cloud is stored as "Multi Cloud" (DB spelling), techs replaced
        await emdt.ClassifyAsync(3, Form() with { Cloud = "Multi-Cloud", Techs = ["COBOL"] }, Sachin);
        await using var ctx2 = db.CreateContext();
        var again = await ctx2.Classifications.AsNoTracking().Include(x => x.ClassificationTechnologies).SingleAsync(x => x.EnterpriseId == 3);
        Assert.Equal("Multi Cloud", again.CloudType);
        Assert.Equal("COBOL", Assert.Single(again.ClassificationTechnologies).Technology);
        Assert.Equal(1, await ctx2.Classifications.CountAsync(x => x.EnterpriseId == 3));
        Assert.Equal(2, await ctx2.AuditLogs.CountAsync(a => a.Action == "Enterprise Classified" && a.Target == "Fabrikam Health"));
    }

    [Fact]
    public async Task Applications_register_update_and_delete_rules()
    {
        var emdt = Emdt;
        var bad = App(2, "x", age: 51);
        bad.Framework = "Java";
        var errors = ModernizationService.CheckApplication(bad, [], "Contoso Retail");
        Assert.Contains(("Application Name", "3-60 characters: letters, numbers, space & . -"), errors);
        Assert.Contains(("Age (years)", "Whole number from 0 to 50"), errors);
        Assert.Contains(("Framework", "Name + version number, e.g. Java 8 or .NET 6"), errors);
        Assert.Contains("This application already exists in Contoso Retail",
            (await Assert.ThrowsAsync<UserFacingException>(() => emdt.SaveApplicationAsync(App(2, "online store"), Sachin))).Message);

        var saved = await emdt.SaveApplicationAsync(App(2), Sachin);
        Assert.StartsWith("APP-0", saved.Code);
        var edit = App(2, "Flight Booking Portal", 7);
        edit.ApplicationId = saved.ApplicationId;
        await emdt.SaveApplicationAsync(edit, Sachin);
        Assert.Equal(("Flight Booking Portal", 7), ((await emdt.ApplicationAsync(saved.ApplicationId))!.Name, (await emdt.ApplicationAsync(saved.ApplicationId))!.AgeYears));

        // APP-001 has assessments: kept. APP-010 (Supplier Portal) has 1 link and no assessment: deleted with it.
        Assert.Contains("cannot be deleted", (await Assert.ThrowsAsync<UserFacingException>(() => emdt.DeleteApplicationAsync(1, Sachin))).Message);
        Assert.Equal(1, await emdt.DeleteApplicationAsync(10, Sachin));
        await using var ctx = db.CreateContext();
        Assert.False(await ctx.Applications.AnyAsync(a => a.ApplicationId == 10));
        Assert.False(await ctx.Dependencies.AnyAsync(d => d.SourceApplicationId == 10));
        Assert.Equal(["Application Registered", "Application Updated", "Application Deleted"],
            await ctx.AuditLogs.Where(a => a.Action.StartsWith("Application ")).OrderBy(a => a.AuditLogId).Select(a => a.Action).ToListAsync());
    }

    [Fact]
    public async Task Components_and_dependency_rules()
    {
        var emdt = Emdt;
        var comps = await emdt.ComponentsAsync(1);
        Assert.Equal("3-60 characters: letters, numbers, space . _ -", ModernizationService.ComponentName("A!", comps));
        Assert.Equal("A component with this name already exists", ModernizationService.ComponentName("trade database", comps));
        Assert.Contains("Select the parent application", (await Assert.ThrowsAsync<UserFacingException>(() => emdt.AddComponentAsync(1, "API", null, "Fx Rates API", Sachin))).Message);
        var fx = await emdt.AddComponentAsync(1, "API", 1, "Fx Rates API", Sachin);   // under Trade Finance Portal
        Assert.Equal("N-20", fx.Code);

        var nodes = await emdt.NodesAsync(1);
        var links = await emdt.LinksAsync(1);
        ModernizationService.Node App(int id) => nodes.Single(n => n.ApplicationId == id);
        ModernizationService.Node Comp(int id) => nodes.Single(n => n.ComponentId == id);
        Assert.Contains(("Target", "Source and target cannot be the same"), ModernizationService.CheckLink(App(3), App(3), "High", null, links, null));
        Assert.Contains(("Target", "A component cannot depend on its own parent application"), ModernizationService.CheckLink(Comp(fx.ComponentId), App(1), "High", null, links, null));
        Assert.Contains(("Target", "This link already exists"), ModernizationService.CheckLink(App(1), Comp(1), "High", null, links, null));   // DEP-001
        Assert.Contains(("Criticality", "Criticality is required"), ModernizationService.CheckLink(App(1), Comp(2), null, null, links, null));
        Assert.Contains(("Source", "Source is required"), ModernizationService.CheckLink(null, Comp(2), "Low", null, links, null));

        var before = EmdtCalc.DepImpact(3, links);   // Payment Gateway
        var dep = await emdt.SaveLinkAsync(1, null, Comp(8), Comp(fx.ComponentId), "Critical", "FX conversion", Sachin);   // Payment Service -> Fx Rates API
        Assert.Equal("DEP-021", dep.Code);
        var after = EmdtCalc.DepImpact(3, await emdt.LinksAsync(1));
        Assert.Equal(before.Raw + 2 * 4, after.Raw);   // API weight 2 x Critical 4
        Assert.Contains("Remove the 1 link(s) using this component first", (await Assert.ThrowsAsync<UserFacingException>(() => emdt.DeleteComponentAsync(fx.ComponentId, Sachin))).Message);

        await emdt.SaveLinkAsync(1, dep.DependencyId, Comp(8), Comp(fx.ComponentId), "Medium", "FX conversion", Sachin);
        await emdt.DeleteLinkAsync(dep.DependencyId, Sachin);
        await emdt.DeleteComponentAsync(fx.ComponentId, Sachin);
        await using var ctx = db.CreateContext();
        Assert.Equal(["Component Added", "Dependency Added", "Dependency Updated", "Dependency Deleted", "Component Deleted"],
            await ctx.AuditLogs.Where(a => a.UserId == Sachin && (a.Action.StartsWith("Component") || a.Action.StartsWith("Dependency")) && a.AuditLogId > 8)
                .OrderBy(a => a.AuditLogId).Select(a => a.Action).ToListAsync());
        Assert.Equal("Payment Service → Fx Rates API", await ctx.AuditLogs.Where(a => a.Action == "Dependency Added" && a.AuditLogId > 8).Select(a => a.Target).SingleAsync());
    }
}
