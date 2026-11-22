using System.Text.RegularExpressions;

namespace SecureShift.ConsoleApp.Services.Governance;

/// <summary>
/// The Cloud Governance formulas. The prototype only SHOWS the result (governance-data.js: the 3 signals
/// "Unused permission ratio 50%", "Unused high-risk actions 30%", "Scope breadth 20%", SS.GOV.riskOf,
/// SS.GOV.riskBand); it never calculates a score from files. SecureShift v7 uses these rules:
///
///   unused ratio   = Math.round((1 - used / granted) * 100)              (same as SS.GOV.getDetail)
///   high-risk      = min(100, unused high-risk actions x 10)
///   scope breadth  = Management Group / Subscription 100, Resource Group 40, Resource 10
///   score          = Math.round((wUnused * ratio + wHighRisk * highRisk + wScope * scope) / 100)
///                    (weights from Rule Settings, they always add up to 100)
///   risk level     = SS.GOV.riskOf: >= 70 HIGH, >= 40 MEDIUM, else LOW (also the DB column RiskLevel)
///
/// With the default weights 50/30/20 this gives the seeded values for the main demo identities,
/// e.g. svc-webapp-deploy 1850 granted / 12 used / 7 high-risk / Subscription -> 91, priya.sharma -> 71.
/// </summary>
public static class GovernanceScoring
{
    public const int HighRiskPointsPerAction = 10;

    /// <summary>SS.GOV.getDetail: Math.round((1 - used / granted) * 100). 0 when nothing is granted.</summary>
    public static int UnusedRatio(int granted, int used) =>
        granted <= 0 ? 0 : JsMath.RoundInt((1 - (double)used / granted) * 100);

    public static int HighRiskSignal(int unusedHighRisk) => Math.Min(100, unusedHighRisk * HighRiskPointsPerAction);

    public static int ScopeSignal(string scope) => scope switch
    {
        "Management Group" or "Subscription" => 100,
        "Resource Group" => 40,
        _ => 10,   // Resource
    };

    public static int Score(int granted, int used, int unusedHighRisk, string scope, int wUnused, int wHighRisk, int wScope)
    {
        var total = wUnused * UnusedRatio(granted, used) + wHighRisk * HighRiskSignal(unusedHighRisk) + wScope * ScopeSignal(scope);
        return Math.Clamp(JsMath.RoundInt(total / 100.0), 0, 100);
    }

    /// <summary>SS.GOV.riskOf (identity score).</summary>
    public static string RiskOf(int score) => score >= 70 ? "HIGH" : score >= 40 ? "MEDIUM" : "LOW";

    /// <summary>SS.GOV.riskBand (company average score).</summary>
    public static string RiskBand(decimal avg) => avg >= 60 ? "HIGH" : avg >= 40 ? "MEDIUM" : "LOW";

    /// <summary>
    /// High-risk pattern match: '*' matches any characters (also '/'), the rest must match exactly,
    /// case-insensitive. "*/delete" matches Microsoft.Compute/virtualMachines/delete.
    /// </summary>
    public static bool Matches(string pattern, string action)
    {
        var rx = "^" + string.Join(".*", pattern.Split('*').Select(Regex.Escape)) + "$";
        return Regex.IsMatch(action, rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// The finding category (the DB allows only the 4 categories of the prototype's Findings page).
    /// Returns null when the action fits none of them: it still counts as an unused high-risk action in the score.
    /// </summary>
    public static string? CategoryOf(string action)
    {
        var a = action.ToLowerInvariant();
        if (a.StartsWith("microsoft.authorization/")) return "Role Assignment";
        if (a.StartsWith("microsoft.keyvault/")) return "Key Vault";
        if (a.StartsWith("microsoft.network/networksecuritygroups/")) return "Network";
        if (a.EndsWith("/delete")) return "Delete";
        if (a.StartsWith("microsoft.network/")) return "Network";
        return null;
    }

    /// <summary>
    /// Finding severity: the same unused action is more dangerous on a wider scope.
    /// Role assignments are always HIGH (they can grant any other permission).
    /// </summary>
    public static string SeverityOf(string category, string scope) =>
        category == "Role Assignment" ? "HIGH" : scope switch
        {
            "Management Group" or "Subscription" => "HIGH",
            "Resource Group" => "MEDIUM",
            _ => "LOW",
        };

    /// <summary>"used once, 85 days ago" (GeneratedRoleActions.Note in the seed).</summary>
    public static string RareNote(int events, int daysAgo)
    {
        var times = events switch { 1 => "used once", 2 => "used twice", _ => $"used {events} times" };
        var when = daysAgo switch { 0 => "today", 1 => "1 day ago", _ => $"{daysAgo} days ago" };
        return $"{times}, {when}";
    }

    /// <summary>Scope breadth text of the identity detail page.</summary>
    public static string ScopeText(string scope, string? suggested) =>
        scope is "Subscription" or "Management Group"
            ? $"{scope} (work in {(suggested is null ? "no" : "1")} resource group)"
            : scope == "Resource Group" ? "Resource group" : "Resource";
}
