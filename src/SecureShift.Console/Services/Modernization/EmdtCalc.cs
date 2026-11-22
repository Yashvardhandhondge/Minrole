using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Services.Modernization;

/// <summary>One line of the EMSI breakdown (emdt-calc.js C.emsi rows).</summary>
public sealed record EmsiRow(string Label, string Input, double Score, double Weight, double Weighted);

/// <summary>A price plan / category (C.CATEGORIES).</summary>
public sealed record EmsiCategory(string Name, double Min, double Max, double Multiplier, long Monthly, long Annual);

/// <summary>The EMSI result: rows, score (2 decimals), category, multiplier, plan.</summary>
public sealed record EmsiResult(IReadOnlyList<EmsiRow> Rows, double Score, EmsiCategory Category);

/// <summary>One assessment factor: the reason for some points (C.techDebt / C.govRisk factors).</summary>
public sealed record Factor(string Name, string Value, int Points);

/// <summary>The numbers of a simulation (C.simulate), from the assessment + EMSI multiplier + strategy.</summary>
public sealed record SimulationCalc(int ModernizationRisk, string Level, double BaseWeeks, int AdjWeeks, int Effort, int Team, double PersonMonths,
    double Labour, double Testing, double Tools, double OtherFixed, double ContingencyPct, double Cost, string Recommendation, int StrategyRisk, double PerPersonMonth);

/// <summary>One plan phase (C.phases).</summary>
public sealed record PhaseCalc(int No, string Name, string Pct, int Weeks);

/// <summary>A target of a direct dependency (C.affected).</summary>
public sealed record AffectedSystem(string Name, string Type, string Criticality, string Owner);

/// <summary>
/// ALL EMDT formulas, ported line by line from js/data/emdt-calc.js (doubles, JavaScript rounding, same order
/// of operations), so the console gives exactly the prototype's numbers. Checked by the tests against the
/// values the prototype JavaScript itself calculates (tests/prototype/prototype-expected.js).
/// </summary>
public static class EmdtCalc
{
    private const double Inf = double.PositiveInfinity;

    /// <summary>Pick the score for a value from a list of [upperLimit, score] bands.</summary>
    private static double Band(double value, (double Upper, double Score)[] bands)
    {
        foreach (var (upper, score) in bands) if (value <= upper) return score;
        return bands[^1].Score;
    }

    private static double Round2(double n) => JsMath.Round2(n);

    /// <summary>C.level: >= 70 High, >= 40 Medium, else Low.</summary>
    public static string Level(double score) => score >= 70 ? "High" : score >= 40 ? "Medium" : "Low";

    /* ===================================================== 1. EMSI */

    public static readonly IReadOnlyDictionary<string, int> TechValues = new Dictionary<string, int> { ["Java"] = 75, ["PHP"] = 95, [".NET"] = 120, ["COBOL"] = 450 };
    public static readonly string[] Technologies = ["Java", "PHP", ".NET", "COBOL"];
    public static readonly string[] Policies = ["GDPR", "HIPAA", "PCI DSS", "ISO 27001", "SOC 2"];

    /// <summary>The 4 cloud types of the prototype form. The DB spells the last one "Multi Cloud" (no hyphen).</summary>
    public static readonly string[] CloudTypes = ["Public Cloud", "Private Cloud", "Hybrid Cloud", "Multi-Cloud"];
    private static readonly Dictionary<string, int> CloudScores = new() { ["Public Cloud"] = 25, ["Private Cloud"] = 40, ["Hybrid Cloud"] = 70, ["Multi-Cloud"] = 100 };
    public static string CloudToDb(string prototypeName) => prototypeName == "Multi-Cloud" ? "Multi Cloud" : prototypeName;
    public static string CloudFromDb(string dbName) => dbName == "Multi Cloud" ? "Multi-Cloud" : dbName;

    public static double ScoreTotalApps(double v) => Band(v, [(25, 20), (100, 40), (250, 60), (500, 80), (Inf, 100)]);
    public static double ScoreDeps(double v) => v == 0 ? 0 : Band(v, [(25, 25), (100, 50), (300, 75), (Inf, 100)]);
    public static double ScoreApis(double v) => v == 0 ? 0 : Band(v, [(10, 25), (50, 50), (150, 75), (Inf, 100)]);
    public static double ScoreDbs(double v) => Band(v, [(5, 25), (15, 50), (30, 75), (Inf, 100)]);
    public static double ScoreStorageGb(double v) => Band(v, [(99.999, 20), (1024, 40), (5120, 60), (20480, 80), (Inf, 100)]);
    public static double ScoreEnvs(double v) => Band(v, [(1, 25), (2, 50), (3, 75), (Inf, 100)]);
    public static double ScoreUnits(double v) => Band(v, [(1, 20), (3, 40), (6, 60), (10, 80), (Inf, 100)]);
    public static double ScoreTechs(IEnumerable<string> list) => Round2((list.Sum(x => TechValues.TryGetValue(x, out var t) ? t : 0) / 740.0) * 100);
    public static double ScoreUsers(double v) => Band(v, [(50, 20), (200, 40), (500, 60), (1000, 80), (Inf, 100)]);
    public static double ScoreCrit(int low, int medium, int high, int critical)
    {
        var n = low + medium + high + critical;
        if (n == 0) return 0;
        return Round2((low * 25.0 + medium * 50.0 + high * 75.0 + critical * 100.0) / n);
    }
    public static double ScorePolicies(int count) => Band(count, [(1, 25), (2, 50), (3, 75), (Inf, 100)]);
    public static double ScoreCloud(string type) => CloudScores.TryGetValue(CloudFromDb(type), out var s) ? s : 0;   // On-Premise: 0 (like C.score.cloud)
    public static double ScoreLoc(double v) => Band(v, [(99999, 20), (500000, 40), (2000000, 60), (10000000, 80), (Inf, 100)]);

    /// <summary>C.EMSI_ROWS: the 12 parameters and their weights (they add up to 100%).</summary>
    public static readonly (string Key, string Label, double Weight)[] EmsiRows =
    [
        ("totalApps", "Total applications", 0.14), ("deps", "Number of dependencies", 0.14), ("apis", "Number of APIs", 0.07),
        ("dbStorage", "Databases and storage", 0.09), ("envs", "Number of environments", 0.06), ("units", "Number of business units", 0.04),
        ("techs", "Technology diversity", 0.15), ("users", "Number of active users", 0.04), ("crit", "Application criticality", 0.09),
        ("policies", "Governance policies used", 0.06), ("cloud", "Cloud infrastructure type", 0.07), ("loc", "Lines of code", 0.05),
    ];

    /// <summary>C.CATEGORIES (pricing in rupees).</summary>
    public static readonly EmsiCategory[] Categories =
    [
        new("Foundation", 0, 24.99, 0.90, 5000, 54000),
        new("Growth", 25, 44.99, 1.00, 10000, 108000),
        new("Enterprise", 45, 69.99, 1.05, 25000, 270000),
        new("Strategic", 70, 100, 1.10, 50000, 540000),
    ];

    public static EmsiCategory CategoryOf(double score)
    {
        for (var i = Categories.Length - 1; i >= 0; i--) if (score >= Categories[i].Min) return Categories[i];
        return Categories[0];
    }

    /// <summary>C.emsi(cls) for a dev."Classifications" row (+ its technologies and policies).</summary>
    public static EmsiResult Emsi(Classification c, IReadOnlyList<string> techs, IReadOnlyList<string> policies)
    {
        var dbScore = ScoreDbs(c.DatabaseCount);
        var stScore = ScoreStorageGb(c.StorageGb);
        var orderedTechs = Technologies.Where(techs.Contains).ToList();   // the form lists them in this order
        var orderedPolicies = Policies.Where(policies.Contains).Concat(policies.Where(p => !Policies.Contains(p))).ToList();
        var inputs = new Dictionary<string, (string Input, double Score)>
        {
            ["totalApps"] = (c.TotalApps.ToString(), ScoreTotalApps(c.TotalApps)),
            ["deps"] = (c.Dependencies.ToString(), ScoreDeps(c.Dependencies)),
            ["apis"] = (c.Apis.ToString(), ScoreApis(c.Apis)),
            ["dbStorage"] = ($"{c.DatabaseCount} DBs, {Fmt.Indian(c.StorageGb)} GB", Round2((dbScore + stScore) / 2)),
            ["envs"] = (c.Environments.ToString(), ScoreEnvs(c.Environments)),
            ["units"] = (c.BusinessUnits.ToString(), ScoreUnits(c.BusinessUnits)),
            ["techs"] = (string.Join(" + ", orderedTechs), ScoreTechs(orderedTechs)),
            ["users"] = (Fmt.Indian(c.UserCount), ScoreUsers(c.UserCount)),
            ["crit"] = ($"{c.CritLow}L / {c.CritMedium}M / {c.CritHigh}H / {c.CritCritical}C", ScoreCrit(c.CritLow, c.CritMedium, c.CritHigh, c.CritCritical)),
            ["policies"] = (orderedPolicies.Count > 0 ? string.Join(", ", orderedPolicies) : "None", ScorePolicies(orderedPolicies.Count)),
            ["cloud"] = (CloudFromDb(c.CloudType), ScoreCloud(c.CloudType)),
            ["loc"] = (Fmt.Indian(c.LinesOfCode), ScoreLoc(c.LinesOfCode)),
        };
        double total = 0;
        var rows = new List<EmsiRow>();
        foreach (var (key, label, weight) in EmsiRows)
        {
            var sc = inputs[key].Score;
            rows.Add(new EmsiRow(label, inputs[key].Input, sc, weight, Round2(sc * weight)));
            total += sc * weight;
        }
        var score = Round2(total);
        return new EmsiResult(rows, score, CategoryOf(score));
    }

    /* ===================================================== 2. Dependencies */

    public static readonly IReadOnlyDictionary<string, int> TypeWeight = new Dictionary<string, int> { ["Application"] = 4, ["Service"] = 3, ["Database"] = 3, ["API"] = 2 };
    public static readonly IReadOnlyDictionary<string, int> CritPoints = new Dictionary<string, int> { ["Low"] = 1, ["Medium"] = 2, ["High"] = 3, ["Critical"] = 4 };
    public static readonly IReadOnlyDictionary<string, int> CritRank = new Dictionary<string, int> { ["None"] = 0, ["Low"] = 1, ["Medium"] = 2, ["High"] = 3, ["Critical"] = 4 };
    public const double DiK = 20;
    public static readonly string[] Criticalities = ["Low", "Medium", "High", "Critical"];

    /// <summary>The type of a link = the type of its TARGET (Application, Service, Database, API).</summary>
    public static string LinkType(Dependency d) => d.TargetComponent?.ComponentType ?? "Application";

    /// <summary>C.linkPoints: TYPE_WEIGHT[type] x CRIT_POINTS[criticality].</summary>
    public static int LinkPoints(Dependency d) => TypeWeight[LinkType(d)] * CritPoints[d.Criticality];

    /// <summary>The application that owns a link end (the application itself, or the component's application).</summary>
    public static int? SourceOwner(Dependency d) => d.SourceApplicationId ?? d.SourceComponent?.ApplicationId;
    public static int? TargetOwner(Dependency d) => d.TargetApplicationId ?? d.TargetComponent?.ApplicationId;

    /// <summary>C.appLinks: the direct OUTGOING links of an application (from the app or any of its components).</summary>
    public static List<Dependency> AppLinks(int applicationId, IEnumerable<Dependency> links) =>
        links.Where(l => SourceOwner(l) == applicationId).OrderBy(l => l.DependencyId).ToList();

    /// <summary>C.depImpact: 100 x (1 - e^(-raw / K)), rounded.</summary>
    public static int DepImpactScore(int raw) => JsMath.RoundInt(100 * (1 - Math.Exp(-raw / DiK)));

    public static (int Count, int Raw, int Score) DepImpact(int applicationId, IEnumerable<Dependency> links)
    {
        var mine = AppLinks(applicationId, links);
        var raw = mine.Sum(LinkPoints);
        return (mine.Count, raw, DepImpactScore(raw));
    }

    public static string TargetName(Dependency d) => d.TargetComponent?.Name ?? d.TargetApplication?.Name ?? "?";
    public static string SourceName(Dependency d) => d.SourceComponent?.Name ?? d.SourceApplication?.Name ?? "?";

    /// <summary>C.affected: the targets of the direct links; owner = the target's application when it is another one.</summary>
    public static List<AffectedSystem> Affected(int applicationId, IEnumerable<Dependency> links, IReadOnlyDictionary<int, string> appNames) =>
        AppLinks(applicationId, links).Select(l =>
        {
            var owner = TargetOwner(l);
            return new AffectedSystem(TargetName(l), LinkType(l), l.Criticality,
                owner is not null && owner != applicationId && appNames.TryGetValue(owner.Value, out var n) ? n : "Same application");
        }).ToList();

    /* ===================================================== 3. Assessment */

    /// <summary>C.techDebt.</summary>
    public static (int Score, List<Factor> Factors) TechDebt(Application a)
    {
        var f = new List<Factor>
        {
            new("Application age", a.AgeYears + " years", Math.Min(25, a.AgeYears * 2)),
            new("Framework support", $"{a.Framework} ({a.SupportStatus})", a.SupportStatus switch { "Unsupported" => 25, "Extended" => 12, _ => 0 }),
            new("Documentation quality", a.DocQuality, a.DocQuality switch { "Poor" => 15, "Average" => 8, _ => 0 }),
            new("Code complexity", a.Complexity, a.Complexity switch { "High" => 20, "Medium" => 10, "Low" => 3, _ => 0 }),
            new("Codebase size", Fmt.Indian(a.LinesOfCode) + " lines", (int)Band(a.LinesOfCode, [(99999, 2), (500000, 5), (2000000, 10), (Inf, 15)])),
        };
        return (Math.Min(100, f.Sum(x => x.Points)), f);
    }

    /// <summary>C.govRisk (needs the application's outgoing links for "Dependency criticality").</summary>
    public static (int Score, List<Factor> Factors) GovRisk(Application a, IReadOnlyList<Dependency> outgoing)
    {
        var top = outgoing.Aggregate("None", (m, l) => CritRank[l.Criticality] > CritRank[m] ? l.Criticality : m);
        if (outgoing.Count == 0) top = "None";
        var f = new List<Factor>
        {
            new("Business criticality", a.Criticality, a.Criticality switch { "Critical" => 30, "High" => 22, "Medium" => 12, "Low" => 5, _ => 0 }),
            new("Sensitive data", a.HasSensitiveData ? "Yes" : "No", a.HasSensitiveData ? 20 : 0),
            new("Security issues", a.SecurityIssues + " issues", (int)Band(a.SecurityIssues, [(0, 0), (2, 5), (5, 10), (Inf, 15)])),
            new("Dependency criticality", top == "None" ? "No dependencies" : "Highest: " + top, top switch { "Critical" => 15, "High" => 10, "Medium" => 5, "Low" => 2, _ => 0 }),
            new("Downtime tolerance", a.DowntimeTolerance, a.DowntimeTolerance switch { "Low" => 10, "Medium" => 5, _ => 0 }),
            new("Compliance requirements", a.CompliancePolicies + (a.CompliancePolicies == 1 ? " policy" : " policies"), (int)Band(a.CompliancePolicies, [(0, 0), (2, 5), (Inf, 10)])),
        };
        return (Math.Min(100, f.Sum(x => x.Points)), f);
    }

    /* ===================================================== 4. Simulation */

    public sealed record Strategy(string Name, int Risk, int Weeks, int Team, string Label);

    public static readonly Strategy[] Strategies =
    [
        new("Rehost", 25, 1, 3, "Move as-is to the cloud"),
        new("Replatform", 50, 2, 4, "Small changes to use a managed platform"),
        new("Refactor", 75, 4, 5, "Restructure the code for the cloud"),
        new("Rebuild", 100, 6, 6, "Rewrite the application from scratch"),
    ];

    public static Strategy StrategyOf(string name) => Strategies.First(s => s.Name == name);

    // C.COST (Rs lakh) and C.MR_WEIGHTS
    public const double PerPersonMonth = 0.60, TestingPct = 0.10, Tools = 1.00, OtherFixed = 1.50, ContingencyPct = 0.15;
    private const double WTd = 0.30, WGr = 0.25, WDi = 0.25, WSr = 0.20;

    /// <summary>The multiplier of an EMSI score (Not classified = 1).</summary>
    public static double MultiplierOf(decimal? emsi) => emsi is null ? 1 : CategoryOf((double)emsi.Value).Multiplier;

    public static string CategoryNameOf(decimal? emsi) => emsi is null ? "Not classified" : CategoryOf((double)emsi.Value).Name;

    /// <summary>C.simulate (the numbers): assessment TD / GR / DI + strategy + EMSI multiplier.</summary>
    public static SimulationCalc Simulate(int td, int gr, int di, string strategy, double multiplier)
    {
        var st = StrategyOf(strategy);
        var mr = JsMath.RoundInt(td * WTd + gr * WGr + di * WDi + st.Risk * WSr);
        var baseWeeks = Round2(6 + mr * 0.12);
        var effort = JsMath.RoundInt(baseWeeks * multiplier + st.Weeks);
        var pm = Round2(effort / 4.33);
        var labour = Round2(st.Team * pm * PerPersonMonth);
        var testing = Round2(labour * TestingPct);
        var subtotal = labour + testing + Tools + OtherFixed;
        var cost = Round2(subtotal * (1 + ContingencyPct));
        var level = Level(mr);
        var rec = level == "High"
            ? "High modernization risk. Use a phased implementation with a parallel run. Stabilise the critical dependencies first."
            : level == "Medium"
                ? "Moderate risk. Proceed in planned releases with dependency testing at every step."
                : "Low risk. Good candidate for a single-release modernization.";
        return new SimulationCalc(mr, level, baseWeeks, st.Weeks, effort, st.Team, pm, labour, testing, Tools, OtherFixed, ContingencyPct, cost, rec, st.Risk, PerPersonMonth);
    }

    /// <summary>The suggested migration order: most critical dependency first (stable sort, like the prototype).</summary>
    public static List<AffectedSystem> Sequence(IEnumerable<AffectedSystem> affected) =>
        affected.OrderByDescending(x => CritRank.TryGetValue(x.Criticality, out var r) ? r : 0).ToList();

    /* ===================================================== 5. Plan phases */

    public static readonly string[] PhaseNames = ["Assessment and Readiness", "Architecture and Design", "Implementation", "Testing and Validation"];

    /// <summary>C.phases: 15% / 20% / 45% / remainder (the rounding difference goes to the last phase).</summary>
    public static List<PhaseCalc> Phases(int effort)
    {
        var p1 = JsMath.RoundInt(effort * 0.15);
        var p2 = JsMath.RoundInt(effort * 0.20);
        var p3 = JsMath.RoundInt(effort * 0.45);
        var p4 = effort - p1 - p2 - p3;
        return [new(1, PhaseNames[0], "15%", p1), new(2, PhaseNames[1], "20%", p2), new(3, PhaseNames[2], "45%", p3), new(4, PhaseNames[3], "Remaining", p4)];
    }
}
