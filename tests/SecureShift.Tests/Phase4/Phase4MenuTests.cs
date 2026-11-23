using Microsoft.EntityFrameworkCore;

namespace SecureShift.Tests.Phase4;

/// <summary>The Admin menus driven by scripted keyboard input, against a fresh database.</summary>
public class Phase4MenuTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private static readonly string[] Login = ["1", "sachin.kumar@secureshift.io", "Admin@2026"];
    private static readonly string[] Logout = ["0", "y", "0"];

    [Fact]
    public async Task Register_classify_add_application_component_link_and_see_the_twin()
    {
        var (code, output) = await db.RunAppAsync([
            .. Login,
            "1", "0",                                                                                 // dashboard
            "2", "Tailspin Airlines", "8", "Pune", "Kavita Desai", "kavita.desai@tailspin.com", "9812300011", // register (becomes current)
            "5", "12", "20", "8", "250000", "3", "400", "2", "1", "2", "30", "1,3", "2,4", "3", "4", "3", "2",   // classify
            "6", "R", "Flight Booking", "Sales", "Web Team", "1", "3", "1", "Java 17", "1", "6", "1", "2", "300000", "3", "1", "1", "1", "1",
            "0",                                                                                      // back from inventory
            "7", "A", "1", "1", "Booking Database",                                                   // add component (Service under Flight Booking)
            "L", "1", "2", "4", "Stores bookings",                                                    // link app -> component, Critical
            "I", "0",                                                                                 // impact results, back
            "8", "0",                                                                                 // digital twin
            .. Logout]);
        Assert.Equal(0, code);
        Assert.Contains("Tailspin Airlines registered as ENT-004", output);
        Assert.Contains("[OK] EMSI 32.33 - Growth category", output);
        Assert.Contains("Flight Booking registered as APP-011", output);
        Assert.Contains("Service Booking Database added (N-20)", output);
        Assert.Contains("Dependency DEP-021 saved", output);
        Assert.Contains("Dependency Impact = 100 x (1 - e^(-raw / 20))", output);
        Assert.Contains("[APP-011] Flight Booking  (Application · High · Dependency Impact 45/100)", output);   // 3 x 4 = 12 raw -> 45
        Assert.Contains("depends on → Booking Database (Service)  [Critical]  DEP-021", output);
        Assert.DoesNotContain("Exception", output);

        await using var ctx = db.CreateContext();
        Assert.Equal(1, await ctx.Dependencies.CountAsync(d => d.EnterpriseId == 4));
    }

    [Fact]
    public async Task Validation_errors_are_shown_and_the_question_is_asked_again()
    {
        var (_, output) = await db.RunAppAsync([
            .. Login,
            "2", "X", "Contoso Retail", "Eastwind Foods", "", "9", "1", "P1", "Pune", "Asha Rao", "asha", "asha@eastwind.com", "123", "9812300099",
            "4", "0",
            "3", "1",                                 // current enterprise: Northwind Bank
            "7", "L", "1", "1", "2", "", "n", "0",   // a link from an application to itself is refused
            .. Logout]);
        Assert.Contains("[ERROR] 3-100 characters: letters, numbers, space, & . -", output);
        Assert.Contains("[ERROR] This enterprise is already registered", output);
        Assert.Contains("[ERROR] Select an industry", output);        // empty answer
        Assert.Contains("[ERROR] Type a number from 1 to 8", output); // wrong number
        Assert.Contains("[ERROR] Letters only, 3-50 characters", output);
        Assert.Contains("[ERROR] Enter a valid email", output);
        Assert.Contains("[ERROR] 10 digits starting with 6, 7, 8 or 9", output);
        Assert.Contains("Eastwind Foods registered as", output);
        Assert.Contains("Not classified yet.", output);
        Assert.Contains("[ERROR] Target: Source and target cannot be the same", output);
    }
}
