using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace SecureShift.Tests.Phase2;

/// <summary>The real menus driven by scripted keyboard input (IConsoleIO), against a fresh database.</summary>
public class Phase2MenuTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private static readonly string[] SuperAdminLogin = ["1", "arya.mujumdar@secureshift.io", "Super@2026"];

    [Fact]
    public async Task End_of_input_exits_cleanly_with_code_0()
    {
        var (code, output) = await db.RunAppAsync("1", "arya.mujumdar@secureshift.io");   // input stops at the password
        Assert.Equal(0, code);
        Assert.Contains("Input ended. Goodbye.", output);
    }

    [Fact]
    public async Task Bad_input_never_crashes()
    {
        var (code, output) = await db.RunAppAsync([.. SuperAdminLogin, "9", "abc", "", "2", "X", "123", "0", "0", "y", "7", "0"]);
        Assert.Equal(0, code);
        Assert.Contains("Type a number from 0 to 5.", output);
        Assert.Contains("Unknown choice.", output);
        Assert.Contains("Type 1, 2 or 0.", output);
        Assert.Contains("Goodbye.", output);
        Assert.DoesNotContain("Exception", output);
    }

    [Fact]
    public async Task Wrong_password_shows_the_generic_message()
    {
        var (_, output) = await db.RunAppAsync("1", "arya.mujumdar@secureshift.io", "nope", "0");
        Assert.Contains("[ERROR] Invalid email or password.", output);
        Assert.Contains("****\n", output);   // the password "nope" is masked
        Assert.DoesNotContain("nope", output);
    }

    [Fact]
    public async Task Each_role_sees_only_its_own_menu()
    {
        var (_, arch) = await db.RunAppAsync("1", "amanjeet.sharma@secureshift.io", "Architect@2026", "0", "y", "0");
        Assert.Contains("Architect", arch);
        Assert.DoesNotContain("User Management", arch);

        var (_, sa) = await db.RunAppAsync([.. SuperAdminLogin, "0", "y", "0"]);
        Assert.Contains("User Management", sa);
        Assert.Contains("Audit Logs", sa);
        Assert.Contains("[OK] You have been logged out.", sa);
    }

    [Fact]
    public async Task Super_Admin_adds_a_user_who_then_must_change_the_temporary_password()
    {
        var (_, output) = await db.RunAppAsync([.. SuperAdminLogin, "2", "A", "Neel Joshi", "neel.joshi@company.com", "9812345670", "3", "0", "0", "y", "0"]);
        Assert.Contains("User Created", output);
        Assert.Contains("Share these details with the user yourself. No email is sent.", output);
        var temp = Regex.Match(output, "Temporary password : (Temp@[0-9]{4}x)").Groups[1].Value;
        Assert.NotEmpty(temp);

        // first login: forced change, the new password must follow the rules and differ from the temporary one
        var (_, first) = await db.RunAppAsync("1", "neel.joshi@company.com", temp, temp, "weak", "weak",
            temp, "Neel@2026x", "Neel@2026x", "0", "y", "0");
        Assert.Contains("You are using a temporary password", first);
        Assert.Contains("Min 8 characters with uppercase, lowercase, number and special character", first);
        Assert.Contains("[OK] Password updated.", first);
        Assert.Contains("Logged in as Neel Joshi (Architect)", first);

        await using var ctx = db.CreateContext();
        var u = await ctx.Users.AsNoTracking().SingleAsync(x => x.Email == "neel.joshi@company.com");
        Assert.False(u.MustChangePassword);
    }

    [Fact]
    public async Task A_user_deactivated_mid_session_is_logged_out_on_the_next_action()
    {
        var steps = new List<object>
        {
            "1", "sneha.gupta@company.com", "Welcome@2026",
            "Welcome@2026", "Sneha@2026x", "Sneha@2026x",   // forced change
            (Func<string>)(() =>
            {
                // like a Super Admin (or SQLTools) deactivating her while she is logged in
                db.SqlAsync("UPDATE dev.\"Users\" SET \"Status\" = 'Inactive' WHERE \"UserId\" = 10").GetAwaiter().GetResult();
                return "1";
            }),
            "0",
        };
        var (code, output) = await db.RunAppAsync(steps);
        Assert.Equal(0, code);
        Assert.Contains("Your account is no longer active. You have been logged out.", output);
    }

    [Fact]
    public async Task Changes_made_in_SQL_show_on_the_next_screen()
    {
        List<object> steps =
        [
            .. SuperAdminLogin, "2",
            (Func<string>)(() =>
            {
                db.SqlAsync("UPDATE dev.\"Users\" SET \"FullName\" = 'Tejas Pathak Live' WHERE \"UserId\" = 3").GetAwaiter().GetResult();
                return "C";   // redraw the list
            }),
            "0", "0", "y", "0",
        ];
        var (_, output) = await db.RunAppAsync(steps);
        Assert.Contains("Tejas Pathak Live", output);
    }

    [Fact]
    public async Task Audit_log_screen_filters_by_action()
    {
        var (_, output) = await db.RunAppAsync([.. SuperAdminLogin, "3", "A", "Plan Approved", "0", "0", "y", "0"]);
        Assert.Contains("Action: Plan Approved", output);
        Assert.Contains("PLAN-004", output);
    }
}
