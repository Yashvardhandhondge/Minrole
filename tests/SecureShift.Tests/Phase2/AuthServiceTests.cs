using Microsoft.EntityFrameworkCore;
using SecureShift.ConsoleApp.Services;

namespace SecureShift.Tests.Phase2;

/// <summary>Login / logout / password change / session re-check against a real (fresh) PostgreSQL database.</summary>
public class AuthServiceTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private AuthService Auth => db.Get<AuthService>();

    [Theory]
    [InlineData("aarav.mehta@secureshift.io", "Cloud@2026", "cloudadmin")]
    [InlineData("arya.mujumdar@secureshift.io", "Super@2026", "superadmin")]
    [InlineData("sachin.kumar@secureshift.io", "Admin@2026", "admin")]
    [InlineData("amanjeet.sharma@secureshift.io", "Architect@2026", "architect")]
    [InlineData("shivanand.kumatekar@secureshift.io", "Exec@2026", "executive")]
    public async Task The_5_accounts_log_in_and_get_their_role(string email, string password, string role)
    {
        var r = await Auth.LoginAsync(email, password);
        Assert.Equal(LoginStatus.Success, r.Status);
        Assert.Equal(role, r.RoleCode);
        Assert.False(r.MustChangePassword);

        await using var ctx = db.CreateContext();
        var user = await ctx.Users.AsNoTracking().SingleAsync(u => u.UserId == r.UserId);
        Assert.NotNull(user.LastLoginAt);
        Assert.True(await ctx.AuditLogs.AnyAsync(a => a.UserId == r.UserId && a.Action == "Logged In"));
    }

    [Fact]
    public async Task Email_is_case_insensitive_and_trimmed()
    {
        var r = await Auth.LoginAsync("  ARYA.Mujumdar@SecureShift.IO ", "Super@2026");
        Assert.Equal(LoginStatus.Success, r.Status);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_give_the_same_generic_message()
    {
        var wrong = await Auth.LoginAsync("arya.mujumdar@secureshift.io", "Super@2027");
        var unknown = await Auth.LoginAsync("nobody@secureshift.io", "Super@2026");
        Assert.Equal(LoginStatus.InvalidCredentials, wrong.Status);
        Assert.Equal(LoginStatus.InvalidCredentials, unknown.Status);
        Assert.Equal("Invalid email or password.", wrong.Message);
        Assert.Equal(wrong.Message, unknown.Message);

        await using var ctx = db.CreateContext();
        Assert.True(await ctx.AuditLogs.AnyAsync(a => a.UserId == 1 && a.Action == "Login Failed"));
    }

    [Theory]
    [InlineData("", "x", "Please enter your email and password.")]
    [InlineData("a@b.com", "", "Please enter your email and password.")]
    [InlineData("not-an-email", "x", "Please enter a valid email address.")]
    public async Task Prototype_login_messages(string email, string password, string message)
    {
        var r = await Auth.LoginAsync(email, password);
        Assert.NotEqual(LoginStatus.Success, r.Status);
        Assert.Equal(message, r.Message);
    }

    [Fact]
    public async Task Inactive_users_are_blocked_even_with_the_right_password()
    {
        var r = await Auth.LoginAsync("sakshi.patil@company.com", "Welcome@2026");   // seeded Inactive
        Assert.Equal(LoginStatus.Inactive, r.Status);
        Assert.Null(r.UserId);
        await using var ctx = db.CreateContext();
        Assert.True(await ctx.AuditLogs.AnyAsync(a => a.UserId == 6 && a.Action == "Login Blocked"));
    }

    [Fact]
    public async Task Welcome_users_must_change_their_password()
    {
        var r = await Auth.LoginAsync("tejas.pathak@company.com", "Welcome@2026");
        Assert.Equal(LoginStatus.Success, r.Status);
        Assert.True(r.MustChangePassword);
    }

    [Fact]
    public async Task Legacy_uppercase_SHA256_hash_is_accepted_and_upgraded_to_V3()
    {
        var legacy = PasswordService.LegacySha256Hex("Legacy@2026");
        Assert.Matches("^[0-9A-F]{64}$", legacy);
        await db.SqlAsync($"UPDATE dev.\"Users\" SET \"PasswordHash\" = '{legacy}' WHERE \"UserId\" = 16");   // Shrinivas Naik

        Assert.Equal(LoginStatus.InvalidCredentials, (await Auth.LoginAsync("shrinivas.naik@company.com", "Legacy@2025")).Status);
        var r = await Auth.LoginAsync("shrinivas.naik@company.com", "Legacy@2026");
        Assert.Equal(LoginStatus.Success, r.Status);
        Assert.True(r.HashUpgraded);

        var stored = (string)(await db.ScalarAsync("SELECT \"PasswordHash\" FROM dev.\"Users\" WHERE \"UserId\" = 16"))!;
        Assert.StartsWith("AQAAAAIAAYag", stored);   // Identity V3, PBKDF2-SHA512, 100000 iterations
        var again = await Auth.LoginAsync("shrinivas.naik@company.com", "Legacy@2026");
        Assert.Equal(LoginStatus.Success, again.Status);
        Assert.False(again.HashUpgraded);
    }

    [Fact]
    public async Task Change_password_follows_the_profile_rules()
    {
        var auth = Auth;
        async Task<string> Fails(string current, string next, string confirm) =>
            (await Assert.ThrowsAsync<UserFacingException>(() => auth.ChangePasswordAsync(15, current, next, confirm, forced: true))).Message;

        Assert.Contains("Current password is wrong", await Fails("Wrong@2026", "Better@2026", "Better@2026"));
        Assert.Contains(Validation.PasswordMessage, await Fails("Welcome@2026", "short", "short"));
        Assert.Contains("Passwords do not match", await Fails("Welcome@2026", "Better@2026", "Better@2027"));
        Assert.Contains("must be different", await Fails("Welcome@2026", "Welcome@2026", "Welcome@2026"));

        await auth.ChangePasswordAsync(15, "Welcome@2026", "Better@2026", "Better@2026", forced: true);   // Mohit Choudhary
        var r = await auth.LoginAsync("mohit.choudhary@company.com", "Better@2026");
        Assert.Equal(LoginStatus.Success, r.Status);
        Assert.False(r.MustChangePassword);
        await using var ctx = db.CreateContext();
        Assert.True(await ctx.AuditLogs.AnyAsync(a => a.UserId == 15 && a.Action == "Password Changed"));
    }

    [Fact]
    public async Task Session_check_logs_out_a_user_deactivated_or_moved_to_another_role_mid_session()
    {
        var auth = Auth;
        var s1 = new Session(17, RoleCodes.Admin, DateTime.UtcNow);        // Pooja Kulkarni, Admin
        Assert.NotNull(await auth.CheckSessionAsync(s1));
        await db.SqlAsync("UPDATE dev.\"Users\" SET \"Status\" = 'Inactive' WHERE \"UserId\" = 17");
        Assert.Null(await auth.CheckSessionAsync(s1));
        Assert.True(s1.Ended);
        Assert.Equal(AuthService.DeactivatedMidSession, s1.EndMessage);

        var s2 = new Session(20, RoleCodes.Admin, DateTime.UtcNow);        // Omkar Jadhav, Admin
        await db.SqlAsync("UPDATE dev.\"Users\" SET \"RoleId\" = 4 WHERE \"UserId\" = 20");   // -> Architect
        Assert.Null(await auth.CheckSessionAsync(s2));
        Assert.Equal(AuthService.RoleChangedMidSession, s2.EndMessage);
    }

    [Fact]
    public async Task Profile_details_use_the_prototype_rules()
    {
        var auth = Auth;
        var ex = await Assert.ThrowsAsync<UserFacingException>(() => auth.UpdateProfileAsync(21, "Is", "5123456789", "IT"));
        Assert.Contains(Validation.NameMessage, ex.Message);
        Assert.Contains(Validation.PhoneMessage, ex.Message);
        Assert.Contains(Validation.DeptMessage, ex.Message);

        await auth.UpdateProfileAsync(21, "Isha Deshpande Rao", "9988776655", "Enterprise Architecture");
        var u = await auth.GetUserAsync(21);
        Assert.Equal("Isha Deshpande Rao", u!.FullName);
        Assert.Equal("Enterprise Architecture", u.Department);
    }
}
