using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Models.Generated;

namespace SecureShift.Tests;

/// <summary>The seeded PasswordHash values are ASP.NET Core Identity V3 hashes that PasswordHasher&lt;User&gt; accepts.</summary>
[Collection(DatabaseCollection.Name)]
public class PasswordTests(DatabaseFixture fixture)
{
    private static readonly Dictionary<string, string> NamedAccounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aarav.mehta@secureshift.io"] = "Cloud@2026",          // Cloud Admin
        ["arya.mujumdar@secureshift.io"] = "Super@2026",        // Super Admin
        ["sachin.kumar@secureshift.io"] = "Admin@2026",         // Admin
        ["amanjeet.sharma@secureshift.io"] = "Architect@2026",  // Architect
        ["shivanand.kumatekar@secureshift.io"] = "Exec@2026",   // Executive
    };
    private const string WelcomePassword = "Welcome@2026";

    [Theory]
    [InlineData("aarav.mehta@secureshift.io", "Cloud@2026", "cloudadmin")]
    [InlineData("arya.mujumdar@secureshift.io", "Super@2026", "superadmin")]
    [InlineData("sachin.kumar@secureshift.io", "Admin@2026", "admin")]
    [InlineData("amanjeet.sharma@secureshift.io", "Architect@2026", "architect")]
    [InlineData("shivanand.kumatekar@secureshift.io", "Exec@2026", "executive")]
    [InlineData("rohan.kapoor@company.com", WelcomePassword, "superadmin")]   // a Welcome@2026 user
    public async Task PasswordHasher_verifies_the_seeded_hash(string email, string password, string roleCode)
    {
        await using var db = fixture.CreateContext();
        var user = await db.Users.AsNoTracking().Include(u => u.Role).SingleAsync(u => u.Email == email);
        var hasher = new PasswordHasher<User>();

        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, password));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, user.PasswordHash, password + "x"));
        Assert.Equal(roleCode, user.Role.RoleCode);
    }

    [Fact]
    public async Task All_25_users_have_a_V3_hash_of_their_expected_password()
    {
        await using var db = fixture.CreateContext();
        var users = await db.Users.AsNoTracking().OrderBy(u => u.UserId).ToListAsync();
        var hasher = new PasswordHasher<User>();

        Assert.Equal(25, users.Count);
        foreach (var user in users)
        {
            var expected = NamedAccounts.GetValueOrDefault(user.Email, WelcomePassword);
            Assert.True(Convert.FromBase64String(user.PasswordHash)[0] == 0x01, $"{user.Email}: not an Identity V3 hash");
            Assert.True(hasher.VerifyHashedPassword(user, user.PasswordHash, expected) == PasswordVerificationResult.Success,
                $"{user.Email}: hash does not verify with {expected}");
        }
    }

    [Fact]
    public async Task Only_the_Welcome_users_must_change_their_password()
    {
        await using var db = fixture.CreateContext();
        var users = await db.Users.AsNoTracking().ToListAsync();

        foreach (var user in users)
            Assert.True(user.MustChangePassword == !NamedAccounts.ContainsKey(user.Email), $"{user.Email}: MustChangePassword = {user.MustChangePassword}");
        Assert.Equal(20, users.Count(u => u.MustChangePassword));
    }

    [Fact]
    public async Task Login_view_returns_role_and_home_page()
    {
        await using var db = fixture.CreateContext();
        var login = await db.VwUserLogins.AsNoTracking().SingleAsync(v => v.Email == "amanjeet.sharma@secureshift.io");

        Assert.Equal("USR-013", login.Code);
        Assert.Equal("Architect", login.RoleName);
        Assert.Equal("Modernization", login.Product);
        Assert.Equal("modernization/architect-dashboard.html", login.HomePage);
        Assert.Equal("Active", login.Status);
    }
}
