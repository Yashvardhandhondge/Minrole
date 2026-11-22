using SecureShift.ConsoleApp.Services;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>Profile &amp; Settings (js/pages/profile.js), the same for every role, plus Change Password.</summary>
public sealed class ProfileMenu(ConsoleUi ui, AuthService auth, MenuRunner runner)
{
    /// <summary>The 2 lines every role menu ends with.</summary>
    public IEnumerable<MenuItem> CommonItems(Session session) =>
    [
        new("My Profile", _ => RunAsync(session)),
        new("Change Password", me => ChangePasswordAsync(me)),
    ];

    public Task RunAsync(Session session) =>
        runner.RunAsync(session, "Profile & Settings", me =>
        {
            ShowProfile(me, session);
            var items = new List<MenuItem>
            {
                new("Edit details (name, phone, department)", EditDetailsAsync),
                new("Change password", ChangePasswordAsync),
            };
            // profile.js: the Activity tab exists only in Cloud Governance
            if (me.Role.Product == RoleCodes.ProductGovernance) items.Add(new("My recent activity", ActivityAsync));
            return items;
        }, topLevel: false);

    private void ShowProfile(User me, Session session)
    {
        ui.WriteLine(me.Role.Product == RoleCodes.ProductGovernance
            ? "Manage your account details, password, notifications and see your recent activity."
            : "Manage your account details and password.");
        ui.KeyValues([
            ("Full Name", me.FullName),
            ("Email", me.Email),
            ("Role", me.Role.RoleName),
            ("Product", RoleCodes.ProductTitle(me.Role.Product)),
            ("User ID", me.Code ?? ""),
            ("Phone", me.Phone ?? "-"),
            ("Department", me.Department ?? "-"),
            ("Logged in at", Fmt.DateTime(session.LoginAtUtc)),
        ]);
    }

    private async Task EditDetailsAsync(User me)
    {
        ui.Section("Profile Details");
        ui.WriteLine("Press Enter to keep the value in [brackets]. Type 'cancel' to go back.");
        var name = ui.AskValid("Full Name", Validation.Name, me.FullName);
        var phone = ui.AskValid("Phone", Validation.Phone, me.Phone?.Trim() ?? "");
        var dept = ui.AskValid("Department", Validation.Dept, me.Department ?? "");
        await auth.UpdateProfileAsync(me.UserId, name, phone, dept);
        ui.Ok("Profile updated");
    }

    public async Task ChangePasswordAsync(User me)
    {
        ui.Section("Change Password");
        ui.WriteLine("Minimum 8 characters with an uppercase letter, a lowercase letter, a number and a special character.");
        var current = ui.AskPassword("Current Password: ");
        var newPassword = ui.AskPassword("New Password: ");
        var confirm = ui.AskPassword("Confirm New Password: ");
        await auth.ChangePasswordAsync(me.UserId, current, newPassword, confirm, forced: false);
        ui.Ok("Password updated. Use the new password next time you log in.");
    }

    private async Task ActivityAsync(User me)
    {
        ui.Section("My recent activity");
        var rows = await auth.MyActivityAsync(me.UserId, 10);
        if (rows.Count == 0)
        {
            ui.WriteLine("No activity yet.");
            return;
        }
        ui.Table(["Time", "Action", "Details"],
            rows.Select(r => (IReadOnlyList<string>)[Fmt.DateTime(r.CreatedAt), r.Action, Fmt.Cut((r.Target ?? "") + (string.IsNullOrEmpty(r.Detail) ? "" : " · " + r.Detail), 70)]).ToList());
    }
}
