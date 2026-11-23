using System.Text.Json;
using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Modernization;
using SecureShift.Data.Models.Generated;

namespace SecureShift.Tests.Phase4;

/// <summary>
/// emdt-calc.js formulas: the C# port gives exactly the numbers the prototype JavaScript calculates
/// (Data/prototype-expected.json, made by running js/data/emdt-calc.js in Node). No database needed.
/// </summary>
public class EmdtFormulaTests
{
    private static Classification ToClassification(JsonElement i) => new()
    {
        TotalApps = i.Int("totalApps"), Dependencies = i.Int("deps"), Apis = i.Int("apis"), DatabaseCount = i.Int("dbs"), StorageGb = i.Int("storageGB"),
        Environments = i.Int("envs"), BusinessUnits = i.Int("units"), UserCount = i.Int("users"),
        CritLow = i.GetProperty("crit").Int("low"), CritMedium = i.GetProperty("crit").Int("medium"), CritHigh = i.GetProperty("crit").Int("high"), CritCritical = i.GetProperty("crit").Int("critical"),
        CloudType = EmdtCalc.CloudToDb(i.Str("cloud")), LinesOfCode = i.GetProperty("loc").GetInt64(),
    };

    private static List<string> Strings(JsonElement e, string name) => e.Items(name).Select(x => x.GetString()!).ToList();

    public static IEnumerable<object[]> EmsiCases() =>
        PrototypeData.Emdt.Items("enterprises").Concat(PrototypeData.Emdt.Items("randomEmsi")).Select((c, n) => new object[] { n });

    [Fact]
    public void EMSI_score_category_and_every_row_match_the_prototype_for_152_portfolios()
    {
        var cases = PrototypeData.Emdt.Items("enterprises").Concat(PrototypeData.Emdt.Items("randomEmsi")).ToList();
        Assert.Equal(152, cases.Count);
        foreach (var c in cases)
        {
            var input = c.GetProperty("input");
            var r = EmdtCalc.Emsi(ToClassification(input), Strings(input, "techs"), Strings(input, "policies"));
            Assert.Equal(c.Num("score"), r.Score);
            Assert.Equal(c.Str("category"), r.Category.Name);
            Assert.Equal(c.Num("multiplier"), r.Category.Multiplier);
            var rows = c.Items("rows").ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                Assert.Equal(rows[i].Str("label"), r.Rows[i].Label);
                Assert.Equal(rows[i].Num("score"), r.Rows[i].Score);
                Assert.Equal(rows[i].Num("weighted"), r.Rows[i].Weighted);
            }
        }
    }

    [Fact]
    public void Seed_enterprises_give_the_seeded_EMSI()
    {
        var e = PrototypeData.Emdt.Items("enterprises").ToDictionary(x => x.Str("name"), x => x.Num("score"));
        Assert.Equal(77.87, e["Northwind Bank"]);   // = dev."Classifications"."EmsiScore" in the seed
        Assert.Equal(44.91, e["Contoso Retail"]);
    }

    [Fact]
    public void Category_level_and_dependency_impact_match()
    {
        Assert.All(PrototypeData.Emdt.Items("categoryOf"), c => Assert.Equal(c.Str("category"), EmdtCalc.CategoryOf(c.Num("score")).Name));
        Assert.Equal(PrototypeData.Emdt.Items("level").Select(x => x.GetString()), Enumerable.Range(0, 101).Select(s => EmdtCalc.Level(s)));
        Assert.All(PrototypeData.Emdt.Items("depImpactRaw"), d => Assert.Equal(d.Int("score"), EmdtCalc.DepImpactScore(d.Int("raw"))));
    }

    [Fact]
    public void Technical_debt_and_governance_risk_match_for_150_random_applications()
    {
        foreach (var c in PrototypeData.Emdt.Items("randomApps"))
        {
            var a = c.GetProperty("app");
            var app = new Application
            {
                Name = a.Str("name"), AgeYears = a.Int("age"), Framework = a.Str("framework"), SupportStatus = a.Str("support"), DocQuality = a.Str("docQuality"),
                Complexity = a.Str("complexity"), LinesOfCode = a.GetProperty("loc").GetInt64(), Criticality = a.Str("criticality"), HasSensitiveData = a.Str("sensitive") == "Yes",
                SecurityIssues = a.Int("securityIssues"), DowntimeTolerance = a.Str("downtime"), CompliancePolicies = a.Int("compliance"),
            };
            var (td, tdf) = EmdtCalc.TechDebt(app);
            var (gr, grf) = EmdtCalc.GovRisk(app, []);
            Assert.Equal(c.Int("td"), td);
            Assert.Equal(c.Int("gr"), gr);
            Assert.Equal(c.Items("tdFactors").Select(f => (f[0].GetString(), f[1].GetString(), f[2].GetInt32())), tdf.Select(f => ((string?)f.Name, (string?)f.Value, f.Points)));
            Assert.Equal(c.Items("grFactors").Select(f => (f[0].GetString(), f[1].GetString(), f[2].GetInt32())), grf.Select(f => ((string?)f.Name, (string?)f.Value, f.Points)));
        }
    }

    [Fact]
    public void Simulation_numbers_match_C_simulate_for_every_seed_app_and_strategy()
    {
        var sims = PrototypeData.Emdt.Items("simulations").ToList();
        Assert.Equal(40, sims.Count);
        foreach (var s in sims)
        {
            var r = EmdtCalc.Simulate(s.Int("td"), s.Int("gr"), s.Int("di"), s.Str("strategy"), s.Num("multiplier"));
            var label = $"{s.Str("app")} {s.Str("strategy")}";
            Assert.True(s.Int("mr") == r.ModernizationRisk, label + " mr");
            Assert.Equal(s.Str("mrLevel"), r.Level);
            Assert.True(s.Num("baseWeeks") == r.BaseWeeks, label + " baseWeeks");
            Assert.True(s.Int("effort") == r.Effort, label + " effort");
            Assert.Equal(s.Int("team"), r.Team);
            Assert.True(s.Num("personMonths") == r.PersonMonths, label + " personMonths");
            Assert.True(s.Num("labour") == r.Labour, label + " labour");
            Assert.True(s.Num("testing") == r.Testing, label + " testing");
            Assert.True(s.Num("cost") == r.Cost, label + $" cost {s.Num("cost")} vs {r.Cost}");
            Assert.Equal(s.Str("recommendation"), r.Recommendation);
            Assert.Equal(s.Int("sr"), r.StrategyRisk);
        }
    }

    [Fact]
    public void Plan_phases_always_add_up_to_the_effort_like_C_phases()
    {
        foreach (var p in PrototypeData.Emdt.Items("phases"))
        {
            var effort = p.Int("effort");
            var mine = EmdtCalc.Phases(effort);
            Assert.Equal(p.Items("weeks").Select(w => w.GetInt32()), mine.Select(x => x.Weeks));
            Assert.Equal(p.Items("names").Select(w => w.GetString()), mine.Select(x => x.Name));
            Assert.Equal(effort, mine.Sum(x => x.Weeks));
        }
        Assert.Equal(["Assessment and Readiness", "Architecture and Design", "Implementation", "Testing and Validation"], EmdtCalc.PhaseNames);
    }

    [Fact]
    public void Number_formats_match_SS_fmt_and_C_lakh()
    {
        Assert.All(PrototypeData.Emdt.Items("fmt"), f => Assert.Equal(f.Str("text"), Fmt.Indian(f.GetProperty("n").GetInt64())));
        Assert.All(PrototypeData.Emdt.Items("lakh"), f => Assert.Equal(f.Str("text"), Fmt.Lakh(f.Num("n"))));
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.5, 3)]      // C# Math.Round would give 2 (banker's rounding); JavaScript gives 3
    [InlineData(-0.5, 0)]
    public void JavaScript_rounding(double x, double expected) => Assert.Equal(expected, JsMath.Round(x));
}
