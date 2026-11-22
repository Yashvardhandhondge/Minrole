using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace SecureShift.Tests.Phase6;

/// <summary>Bugs found during the manual run (Phase 6), so they never come back.</summary>
public class ManualRunRegressionTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    [Fact]
    public async Task Edit_user_with_Enter_everywhere_keeps_the_role_and_asks_the_role_only_once()
    {
        // bug: the role question was inside roles.First(...) and was asked again for every role
        var (_, output) = await db.RunAppAsync("1", "arya.mujumdar@secureshift.io", "Super@2026", "2", "5", "1", "", "", "", "", "0", "0", "0", "y", "0");
        Assert.Equal(1, Regex.Matches(output, "Choose 1-4 \\[Executive\\]").Count);
        await using var ctx = db.CreateContext();
        Assert.Equal("Executive", (await ctx.Users.AsNoTracking().Include(u => u.Role).SingleAsync(u => u.UserId == 5)).Role.RoleName);   // Sonal Shende
    }

    [Fact]
    public async Task Wizard_asks_the_company_only_once_even_for_the_second_company()
    {
        // same bug in the Analysis Wizard: choosing company 2 asked the question twice
        var (_, output) = await db.RunAppAsync("1", "aarav.mehta@secureshift.io", "Cloud@2026", "3", "2", "4", "08/12/2026", "11/09/2026",
            Samples.Assignments, Samples.Activity, Samples.Catalog, "n", "0", "y", "0");
        Assert.Equal(1, Regex.Matches(output, "Choose 1-10: ").Count);
        Assert.Contains("Company: Contoso Retail", output);
        Assert.Contains("for Contoso Retail", output);
    }
}
