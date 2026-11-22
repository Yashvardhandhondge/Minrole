using SecureShift.ConsoleApp.Services.Governance;

namespace SecureShift.Tests.Phase3;

/// <summary>Governance formulas compared with the values the prototype JavaScript calculates (no database needed).</summary>
public class GovernanceFormulaTests
{
    [Fact]
    public void Unused_ratio_matches_SS_GOV_getDetail_for_the_128_prototype_identities()
    {
        var all = PrototypeData.Gov.Items("identities").ToList();
        Assert.Equal(128, all.Count);
        Assert.All(all, i => Assert.Equal(i.Int("ratio"), GovernanceScoring.UnusedRatio(i.Int("granted"), i.Int("used"))));
    }

    [Fact]
    public void Risk_level_matches_SS_GOV_riskOf_for_every_score()
    {
        var expected = PrototypeData.Gov.Items("riskOf").Select(x => x.GetString()).ToList();
        Assert.Equal(expected, Enumerable.Range(0, 101).Select(GovernanceScoring.RiskOf));
        Assert.All(PrototypeData.Gov.Items("identities"), i => Assert.Equal(i.Str("riskOf"), GovernanceScoring.RiskOf(i.Int("score"))));
    }

    [Fact]
    public void Company_band_matches_SS_GOV_riskBand()
    {
        Assert.All(PrototypeData.Gov.Items("riskBand"), b => Assert.Equal(b.Str("band"), GovernanceScoring.RiskBand((decimal)b.Num("avg"))));
    }

    [Theory]
    [InlineData(1850, 12, 7, "Subscription", 91)]     // svc-webapp-deploy: seeded score 91
    [InlineData(1850, 95, 5, "Resource Group", 71)]   // priya.sharma: seeded score 71
    [InlineData(100, 100, 0, "Resource", 2)]          // uses everything, narrow scope (only 20% x 10)
    [InlineData(100, 0, 20, "Subscription", 100)]     // uses nothing, 20 high-risk, whole subscription
    public void Score_uses_the_3_prototype_signals_and_the_default_weights(int granted, int used, int highRisk, string scope, int expected)
    {
        Assert.Equal(expected, GovernanceScoring.Score(granted, used, highRisk, scope, 50, 30, 20));
    }

    [Fact]
    public void Weights_change_the_score()
    {
        Assert.Equal(99, GovernanceScoring.Score(1850, 12, 7, "Subscription", 100, 0, 0));   // only the unused ratio
        Assert.Equal(70, GovernanceScoring.Score(1850, 12, 7, "Subscription", 0, 100, 0));   // only high-risk: 7 x 10
        Assert.Equal(0, GovernanceScoring.UnusedRatio(0, 0));
    }

    [Theory]
    [InlineData("*/delete", "Microsoft.Compute/virtualMachines/delete", true)]
    [InlineData("*/delete", "Microsoft.Compute/virtualMachines/write", false)]
    [InlineData("Microsoft.Authorization/*/write", "Microsoft.Authorization/roleAssignments/write", true)]
    [InlineData("Microsoft.Authorization/*/write", "Microsoft.Authorization/roleAssignments/read", false)]
    [InlineData("Microsoft.KeyVault/*", "microsoft.keyvault/vaults/secrets/read", true)]
    [InlineData("Microsoft.Network/networkSecurityGroups/write", "Microsoft.Network/networkSecurityGroups/write", true)]
    [InlineData("Microsoft.Network/networkSecurityGroups/write", "Microsoft.Network/networkSecurityGroups/securityRules/write", false)]
    public void Pattern_wildcards(string pattern, string action, bool match) => Assert.Equal(match, GovernanceScoring.Matches(pattern, action));

    [Theory]
    [InlineData("Microsoft.Authorization/roleAssignments/write", "Role Assignment")]
    [InlineData("Microsoft.KeyVault/vaults/delete", "Key Vault")]
    [InlineData("Microsoft.Network/networkSecurityGroups/delete", "Network")]
    [InlineData("Microsoft.Network/virtualNetworks/delete", "Delete")]
    [InlineData("Microsoft.Compute/disks/delete", "Delete")]
    [InlineData("Microsoft.Network/networkSecurityGroups/securityRules/write", "Network")]
    [InlineData("Microsoft.Sql/servers/write", null)]
    public void Finding_categories_are_the_4_of_the_prototype(string action, string? category) => Assert.Equal(category, GovernanceScoring.CategoryOf(action));

    [Fact]
    public void Severity_and_rare_notes()
    {
        Assert.Equal("HIGH", GovernanceScoring.SeverityOf("Delete", "Subscription"));
        Assert.Equal("MEDIUM", GovernanceScoring.SeverityOf("Delete", "Resource Group"));   // mi-reports-func (rg-reports) in the seed
        Assert.Equal("HIGH", GovernanceScoring.SeverityOf("Role Assignment", "Resource"));
        Assert.Equal("used once, 85 days ago", GovernanceScoring.RareNote(1, 85));          // GeneratedRoleActions seed row 13
        Assert.Equal("used twice, 40 days ago", GovernanceScoring.RareNote(2, 40));          // seed row 16
    }
}
