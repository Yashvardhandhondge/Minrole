using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureShift.ConsoleApp.Services;
using SecureShift.Data.Models.Generated;

namespace SecureShift.Tests.Phase2;

/// <summary>Super Admin user management + audit logs (modernization/js/users.js, audit-logs.js) on a fresh database.</summary>
public class UserAdminServiceTests(FreshDatabase db) : IClassFixture<FreshDatabase>
{
    private const int Arya = 1;   // Super Admin
    private UserAdminService Users => db.Get<UserAdminService>();

    private async Task<int> RoleIdAsync(string code)
    {
        await using var ctx = db.CreateContext();
        return (await ctx.Roles.SingleAsync(r => r.RoleCode == code)).RoleId;
    }

    [Fact]
    public async Task Dashboard_shows_the_prototype_numbers()
    {
        // fresh copy of the seed: count before other tests of this class add users
        var fresh = new FreshDatabase();
        await fresh.InitializeAsync();
        try
        {
            var d = await fresh.Get<UserAdminService>().DashboardAsync();
            Assert.Equal((24, 20, 4), (d.Total, d.Active, d.Inactive));
            Assert.Equal([("Super Admin", 2), ("Admin", 7), ("Architect", 7), ("Executive", 8)], d.ByRole);
            Assert.Equal(5, d.RecentActivity.Count);
            Assert.Equal("User Created", d.RecentActivity[0].Action);      // newest seeded row (11/11 10:24)
            Assert.All(d.AccessChanges, a => Assert.Matches("Role|Created|Deactivated|Activated|Password", a.Action));
        }
        finally
        {
            await fresh.DisposeAsync();
        }
    }

    [Fact]
    public async Task List_paging_search_and_filters()
    {
        var users = Users;
        var all = await users.ListAsync(new UserFilter(), 1);
        Assert.Equal(10, all.Items.Count);
        Assert.True(all.Total >= 24);
        Assert.DoesNotContain(all.Items, u => u.Email == "aarav.mehta@secureshift.io");   // Cloud Admin is not a Modernization user

        var kumar = await users.ListAsync(new UserFilter(Search: "KUMA"), 1);
        Assert.Contains(kumar.Items, u => u.FullName == "Sachin Kumar");
        Assert.Contains(kumar.Items, u => u.FullName == "Shivanand Kumatekar");

        var byPhone = await users.ListAsync(new UserFilter(Search: "9876543222"), 1);
        Assert.Equal("Amanjeet Sharma", Assert.Single(byPhone.Items).FullName);

        var inactiveArchitects = await users.ListAsync(new UserFilter(RoleName: "Architect", Status: "Inactive"), 1);
        Assert.Equal(["Sakshi Patil", "Rohit Kale", "Nikhil Bhosale"], inactiveArchitects.Items.Select(u => u.FullName));
    }

    [Fact]
    public async Task Add_user_validates_like_the_prototype()
    {
        var users = Users;
        var architect = await RoleIdAsync(RoleCodes.Architect);
        async Task<string> Fails(string name, string email, string phone, int? role) =>
            (await Assert.ThrowsAsync<UserFacingException>(() => users.CreateAsync(name, email, phone, role, Arya))).Message;

        Assert.Contains(Validation.NameMessage, await Fails("R2D2", "r2@company.com", "9123456789", architect));
        Assert.Contains(Validation.NameMessage, await Fails("Two  Spaces", "two@company.com", "9123456789", architect));
        Assert.Contains(Validation.EmailMessage, await Fails("Riya Sen", "riya.company.com", "9123456789", architect));
        Assert.Contains(Validation.PhoneMessage, await Fails("Riya Sen", "riya@company.com", "5123456789", architect));
        Assert.Contains(Validation.PhoneMessage, await Fails("Riya Sen", "riya@company.com", "912345678", architect));
        Assert.Contains("Select a role", await Fails("Riya Sen", "riya@company.com", "9123456789", null));
        Assert.Contains(UserAdminService.EmailExistsMessage, await Fails("Riya Sen", "ARYA.MUJUMDAR@secureshift.io", "9123456789", architect));
    }

    [Fact]
    public async Task Add_user_creates_an_active_user_with_a_temporary_password_and_an_audit_row()
    {
        var (user, temp) = await Users.CreateAsync("Kavita Rane", "kavita.rane@company.com", "9001122334", await RoleIdAsync(RoleCodes.Architect), Arya);

        Assert.Matches("^Temp@[1-9][0-9]{3}x$", temp);
        Assert.True(Validation.IsPassword(temp));
        await using var ctx = db.CreateContext();
        var saved = await ctx.Users.AsNoTracking().Include(u => u.Role).SingleAsync(u => u.UserId == user.UserId);
        Assert.Equal("Active", saved.Status);
        Assert.True(saved.MustChangePassword);
        Assert.Equal("Architect", saved.Role.RoleName);
        Assert.Equal(Arya, saved.CreatedBy);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(saved, saved.PasswordHash, temp));
        var log = await ctx.AuditLogs.AsNoTracking().OrderByDescending(a => a.AuditLogId).FirstAsync(a => a.Action == "User Created");
        Assert.Equal(("Kavita Rane", "Role: Architect", Arya), (log.Target, log.Detail, log.UserId));

        var login = await db.Get<AuthService>().LoginAsync("kavita.rane@company.com", temp);
        Assert.Equal(LoginStatus.Success, login.Status);
        Assert.True(login.MustChangePassword);
    }

    [Fact]
    public async Task Edit_and_change_role_write_the_prototype_audit_rows()
    {
        var users = Users;
        var (u, _) = await users.CreateAsync("Edit Me", "edit.me@company.com", "9001122335", await RoleIdAsync(RoleCodes.Admin), Arya);
        await users.UpdateAsync(u.UserId, "Edited Person", "edited.person@company.com", "9001122336", await RoleIdAsync(RoleCodes.Executive), Arya);
        await users.ChangeRoleAsync(u.UserId, await RoleIdAsync(RoleCodes.Architect), "Moved team", Arya);
        var same = await Assert.ThrowsAsync<UserFacingException>(() => users.ChangeRoleAsync(u.UserId, 4, null, Arya));
        Assert.Equal("Role is unchanged", same.Message);

        await using var ctx = db.CreateContext();
        var logs = await ctx.AuditLogs.AsNoTracking().Where(a => a.Target == "Edit Me" || a.Target == "Edited Person").OrderBy(a => a.AuditLogId).ToListAsync();
        Assert.Equal(
            [("User Created", "Role: Admin"), ("Role Changed", "Admin → Executive"), ("User Updated", "Details edited"), ("Role Changed", "Executive → Architect · Moved team")],
            logs.Select(l => (l.Action, l.Detail)));
    }

    [Fact]
    public async Task Super_Admin_rules_and_self_deactivation()
    {
        var users = Users;
        var adminRole = await RoleIdAsync(RoleCodes.Admin);
        // Rohan Kapoor (USR-002) is a Super Admin
        Assert.Equal(UserAdminService.SuperAdminCannotDeactivate,
            (await Assert.ThrowsAsync<UserFacingException>(() => users.SetStatusAsync(2, false, null, Arya))).Message);
        Assert.Equal(UserAdminService.CannotDeactivateSelf,
            (await Assert.ThrowsAsync<UserFacingException>(() => users.SetStatusAsync(Arya, false, null, Arya))).Message);
        Assert.Equal(UserAdminService.SuperAdminRoleLocked,
            (await Assert.ThrowsAsync<UserFacingException>(() => users.ChangeRoleAsync(2, adminRole, null, Arya))).Message);

        // Edit keeps the Super Admin role even if another role is passed
        await users.UpdateAsync(2, "Rohan Kapoor", "rohan.kapoor@company.com", "9876543211", await RoleIdAsync(RoleCodes.Admin), Arya);
        Assert.Equal(RoleCodes.SuperAdmin, (await users.GetAsync(2))!.Role.RoleCode);
    }

    [Fact]
    public async Task Deactivate_activate_and_reset_password()
    {
        var users = Users;
        var auth = db.Get<AuthService>();
        var (u, temp) = await users.CreateAsync("Status Person", "status.person@company.com", "9001122337", await RoleIdAsync(RoleCodes.Executive), Arya);

        await users.SetStatusAsync(u.UserId, false, "Left project", Arya);
        Assert.Equal(LoginStatus.Inactive, (await auth.LoginAsync("status.person@company.com", temp)).Status);
        Assert.Contains("already Inactive", (await Assert.ThrowsAsync<UserFacingException>(() => users.SetStatusAsync(u.UserId, false, null, Arya))).Message);
        await users.SetStatusAsync(u.UserId, true, null, Arya);

        var (_, newTemp) = await users.ResetPasswordAsync(u.UserId, Arya);
        Assert.Equal(LoginStatus.InvalidCredentials, (await auth.LoginAsync("status.person@company.com", temp)).Status);
        var r = await auth.LoginAsync("status.person@company.com", newTemp);
        Assert.Equal(LoginStatus.Success, r.Status);
        Assert.True(r.MustChangePassword);

        await using var ctx = db.CreateContext();
        var logs = await ctx.AuditLogs.AsNoTracking().Where(a => a.Target == "Status Person" && a.UserId == Arya).OrderBy(a => a.AuditLogId).ToListAsync();
        Assert.Equal(
            [("User Created", "Role: Executive"), ("Account Deactivated", "Left project"), ("Account Activated", ""), ("Password Reset", "Temporary password given manually")],
            logs.Select(l => (l.Action, l.Detail ?? "")));
    }

    [Fact]
    public async Task Audit_logs_newest_first_with_filters()
    {
        var audit = db.Get<AuditLogService>();
        var page = await audit.QueryAsync(new AuditFilter(), 1);
        Assert.Equal(10, page.PageSize);
        Assert.True(page.Items.Zip(page.Items.Skip(1)).All(p => p.First.CreatedAt >= p.Second.CreatedAt));

        var resets = await audit.QueryAsync(new AuditFilter(Action: "Password Reset"), 1);
        Assert.All(resets.Items, a => Assert.Equal("Password Reset", a.Action));
        Assert.Contains(resets.Items, a => a.Target == "Aditi Jain");

        var sachin = await audit.QueryAsync(new AuditFilter(UserId: 12), 1);
        Assert.All(sachin.Items, a => Assert.Equal(12, a.UserId));

        // the seeded rows of 11/09/2026 (UTC); local dates around it so the test works in any time zone
        var nov9 = await audit.QueryAsync(new AuditFilter(From: new DateOnly(2026, 11, 8), To: new DateOnly(2026, 11, 10), Search: "plan approved"), 1);
        Assert.Equal("PLAN-004", Assert.Single(nov9.Items).Target);

        Assert.Contains("User Created", await audit.ActionsAsync());
        Assert.Contains(await audit.UsersAsync(), u => u.FullName == "Shivanand Kumatekar");
    }
}
