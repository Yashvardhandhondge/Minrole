using SecureShift.ConsoleApp.Services;
using SecureShift.ConsoleApp.Services.Governance;

namespace SecureShift.Tests.Phase3;

/// <summary>Reading and validating the 3 JSON files of the Analysis Wizard (no database needed).</summary>
public class GovernanceInputTests
{
    private static Task<GovernanceInput> Load(string a, string e, string c, DateOnly? from = null, DateOnly? to = null) =>
        GovernanceInputReader.LoadAsync(a, e, c, from ?? Samples.From, to ?? Samples.To, "Northwind Traders");

    private static string Temp(string name, string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ss-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task The_sample_files_pass_all_4_prototype_checks()
    {
        var input = await Load(Samples.Assignments, Samples.Activity, Samples.Catalog);
        Assert.Equal(20, input.Assignments.Count);
        Assert.Equal(12, input.Roles.Count);
        Assert.Equal(90, input.PeriodDays);
        Assert.Equal(
            [("Schema validation", "All 3 files match the expected JSON structure"),
             ("Missing fields check", "principalId, roleDefinitionId and scope present in every record"),
             ("Duplicate data check", "0 duplicate role assignments found"),
             ("Activity coverage", "Activity Log covers 08/12/2026 - 11/09/2026 (90 days) for Northwind Traders")],
            input.Steps.Select(s => (s.Name, s.Message)));
        Assert.Contains("1 outside the period, 1 not successful, 1 from unknown callers", input.Summary);
        Assert.DoesNotContain(input.Events, e => e.Status != "Succeeded");
    }

    [Fact]
    public async Task Missing_fields_are_reported_with_the_record_number()
    {
        var ex = await Assert.ThrowsAsync<GovernanceInputException>(() => Load(Samples.Path("bad/role-assignments-missing-fields.json"), Samples.Activity, Samples.Catalog));
        Assert.Equal("Missing fields check failed: role-assignments-missing-fields.json record 2 has no principalId, scope", ex.Message);
        Assert.True(ex.Steps[0].Ok);
        Assert.False(ex.Steps[^1].Ok);
    }

    [Fact]
    public async Task Broken_JSON_gives_the_line_number()
    {
        var ex = await Assert.ThrowsAsync<GovernanceInputException>(() => Load(Samples.Assignments, Samples.Path("bad/activity-log-broken.json"), Samples.Catalog));
        Assert.StartsWith("Schema validation failed: activity-log-broken.json is not valid JSON (line 3", ex.Message);
    }

    [Fact]
    public async Task The_wrong_shape_sample_file_is_refused_with_a_clear_message()
    {
        var ex = await Assert.ThrowsAsync<GovernanceInputException>(() => Load(Samples.Path("bad/role-assignments-wrong-shape.json"), Samples.Activity, Samples.Catalog));
        Assert.Equal("Schema validation failed: role-assignments-wrong-shape.json must be a JSON array of role assignments (or an object with a \"value\" array), but it is a JSON object", ex.Message);
    }

    [Fact]
    public async Task Wrong_shape_duplicates_and_unknown_roles()
    {
        var obj = Temp("assignments.json", "{\"hello\": 1}");
        Assert.Contains("must be a JSON array of role assignments", (await Assert.ThrowsAsync<GovernanceInputException>(() => Load(obj, Samples.Activity, Samples.Catalog))).Message);

        var one = File.ReadAllText(Samples.Assignments);
        var first = System.Text.Json.JsonDocument.Parse(one).RootElement[0].GetRawText();
        var dup = Temp("dup.json", $"[{first},{first}]");
        Assert.Contains("Duplicate data check failed: 1 duplicate role assignment(s) found", (await Assert.ThrowsAsync<GovernanceInputException>(() => Load(dup, Samples.Activity, Samples.Catalog))).Message);

        var noRole = Temp("norole.json", "[" + first.Replace("roleDefinitions/", "roleDefinitions/x") + "]");
        var ex = await Assert.ThrowsAsync<GovernanceInputException>(() => Load(noRole, Samples.Activity, Temp("cat.json", "[{\"id\":\"/x\",\"roleName\":\"Other\",\"permissions\":[]}]")));
        Assert.Contains("is not in cat.json", ex.Message);
    }

    [Fact]
    public async Task Activity_coverage_fails_outside_the_events_of_the_file()
    {
        var ex = await Assert.ThrowsAsync<GovernanceInputException>(() => Load(Samples.Assignments, Samples.Activity, Samples.Catalog, new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 20)));
        Assert.StartsWith("Activity coverage failed: the Activity Log has no successful events", ex.Message);
        Assert.Contains("choose a period that covers them", ex.Message);
    }

    [Fact]
    public void Paths_are_checked_with_friendly_messages()
    {
        Assert.Contains("enter the path", Assert.Throws<UserFacingException>(() => GovernanceInputReader.ResolvePath("  ", "Role Assignments (JSON)")).Message);
        Assert.Contains("please choose a .json file", Assert.Throws<UserFacingException>(() => GovernanceInputReader.ResolvePath("notes.txt", "X")).Message);
        Assert.Contains("file not found", Assert.Throws<UserFacingException>(() => GovernanceInputReader.ResolvePath("C:/nope/missing.json", "X")).Message);
        Assert.Equal(Path.GetFullPath(Samples.Catalog), GovernanceInputReader.ResolvePath($"\"{Samples.Catalog}\"", "X"));   // quotes from drag and drop
    }

    [Theory]
    [InlineData("2026-08-12", "2026-11-09", "")]
    [InlineData("2026-11-01", "2026-11-30", "To date cannot be in the future")]
    [InlineData("2026-11-09", "2026-11-01", "From date must be before To date")]
    [InlineData("2026-11-05", "2026-11-09", "Period must be at least 7 days")]
    [InlineData("2026-01-01", "2026-11-09", "Period cannot be longer than 180 days")]
    public void Period_rules_of_analysis_js(string from, string to, string message) =>
        Assert.Equal(message, GovernanceInputReader.CheckPeriod(DateOnly.Parse(from), DateOnly.Parse(to), new DateOnly(2026, 11, 22)));

    [Theory]
    [InlineData("/subscriptions/abc", "Subscription", null)]
    [InlineData("/subscriptions/abc/resourceGroups/rg-sales", "Resource Group", "rg-sales")]
    [InlineData("/subscriptions/abc/resourceGroups/rg-sales/providers/Microsoft.Web/sites/app1", "Resource", "app1")]
    [InlineData("/providers/Microsoft.Management/managementGroups/mg-root", "Management Group", "mg-root")]
    [InlineData("rg-sales", null, null)]
    public void Azure_scopes(string scope, string? type, string? name) => Assert.Equal((type, name), GovernanceInputReader.ParseScope(scope));
}
