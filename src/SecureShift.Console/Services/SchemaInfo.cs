namespace SecureShift.ConsoleApp.Services;

/// <summary>
/// What the SecureShift part of schema dev must contain (same numbers as 03_verify_dev.sql).
/// </summary>
public static class SchemaInfo
{
    public const string Schema = "dev";
    public const string MigrationsTable = "__SchemaMigrations";
    public const int ExpectedForeignKeys = 46;
    public const int SeedTotalRows = 569;

    /// <summary>The 28 tables, in the order 01/02 create and fill them (parents first).</summary>
    public static readonly IReadOnlyList<string> Tables =
    [
        "Roles", "Users", "AuditLogs",
        "Companies", "Analyses", "Identities", "IdentityScores", "PermissionUsage", "Findings",
        "GeneratedRoles", "GeneratedRoleActions", "RuleSettings", "HighRiskPatterns", "Reports", "Notifications",
        "Enterprises", "Classifications", "ClassificationTechnologies", "ClassificationPolicies",
        "Applications", "Components", "Dependencies", "Assessments", "AssessmentFactors",
        "Simulations", "SimulationImpacts", "Plans", "PlanPhases",
    ];

    public static readonly IReadOnlyList<string> Views =
    [
        "vw_UserLogin", "vw_LatestAssessment", "vw_PlanSummary", "vw_IdentityRisk",
    ];

    /// <summary>Rows per table right after 02_seed_data_postgresql_dev.sql.</summary>
    public static readonly IReadOnlyDictionary<string, int> SeedRowCounts = new Dictionary<string, int>
    {
        ["Roles"] = 5, ["Users"] = 25, ["AuditLogs"] = 8,
        ["Companies"] = 10, ["Analyses"] = 4, ["Identities"] = 128, ["IdentityScores"] = 128,
        ["PermissionUsage"] = 12, ["Findings"] = 37, ["GeneratedRoles"] = 5, ["GeneratedRoleActions"] = 16,
        ["RuleSettings"] = 1, ["HighRiskPatterns"] = 4, ["Reports"] = 4, ["Notifications"] = 3,
        ["Enterprises"] = 3, ["Classifications"] = 2, ["ClassificationTechnologies"] = 5, ["ClassificationPolicies"] = 6,
        ["Applications"] = 10, ["Components"] = 19, ["Dependencies"] = 20, ["Assessments"] = 5,
        ["AssessmentFactors"] = 55, ["Simulations"] = 6, ["SimulationImpacts"] = 18, ["Plans"] = 6, ["PlanPhases"] = 24,
    };

    /// <summary>Quoted, schema-qualified name, e.g. dev."Users". Only used with the fixed names above.</summary>
    public static string Qualified(string name) => $"{Schema}.\"{name.Replace("\"", "\"\"")}\"";
}
