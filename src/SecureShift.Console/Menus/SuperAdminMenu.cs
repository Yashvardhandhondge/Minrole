using SecureShift.ConsoleApp.Services;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// Super Admin (EMDT): Dashboard, User Management, Audit Logs
/// (modernization/js/superadmin-dashboard.js, users.js, audit-logs.js).
/// </summary>
public sealed class SuperAdminMenu(ConsoleUi ui, MenuRunner runner, ProfileMenu profile, UserAdminService users, AuditLogService audit)
{
    private const int PageSize = 10;

    public Task RunAsync(Session session) =>
        runner.RunAsync(session, "Super Admin", me =>
        [
            new("Dashboard", DashboardAsync),
            new("User Management", m => UsersAsync(session, m)),
            new("Audit Logs", AuditLogsAsync),
            .. profile.CommonItems(session),
        ], topLevel: true);

    /* ------------------------------------------------------------------ dashboard */

    private async Task DashboardAsync(User me)
    {
        var d = await users.DashboardAsync();
        ui.Section("Super Admin Dashboard");
        ui.KeyValues([("Total Users", d.Total.ToString()), ("Active Users", d.Active.ToString()), ("Inactive Users", d.Inactive.ToString())]);

        ui.Section("Users by role");
        var max = d.ByRole.Count == 0 ? 0 : d.ByRole.Max(r => r.Count);
        ui.Table(["Role", "Users", ""], d.ByRole.Select(r => (IReadOnlyList<string>)[r.Role, r.Count.ToString(), ConsoleUi.Bar(r.Count, max)]).ToList(), 1);

        ui.Section("Recent activity");
        if (d.RecentActivity.Count == 0) ui.WriteLine("No activity yet.");
        foreach (var l in d.RecentActivity)
            ui.WriteLine($"  {l.Action} · {l.Target}   ({l.User.FullName} · {Fmt.DateTime(l.CreatedAt)})");

        ui.Section("Recent access changes");
        if (d.AccessChanges.Count == 0) ui.WriteLine("No access changes yet.");
        else
            ui.Table(["User Name", "Action", "Details", "Changed By", "Updated On"],
                d.AccessChanges.Select(l => (IReadOnlyList<string>)[l.Target ?? "", l.Action, Fmt.Cut(l.Detail, 40), l.User.FullName, Fmt.DateTime(l.CreatedAt)]).ToList());
    }

    /* ------------------------------------------------------------------ users */

    private async Task UsersAsync(Session session, User me)
    {
        var filter = new UserFilter();
        var page = 1;
        while (true)
        {
            var result = await users.ListAsync(filter, page, PageSize);
            page = result.Page;
            ui.Section($"User Management - Users ({result.Total})");
            ui.WriteLine($"Search: {filter.Search ?? "(none)"} · Role: {filter.RoleName ?? "All"} · Status: {filter.Status ?? "All"}");
            ui.Table(["#", "ID", "Full Name", "Email", "Phone Number", "Role", "Status"],
                result.Items.Select((u, i) => (IReadOnlyList<string>)[(i + 1).ToString(), u.Code ?? "", u.FullName, u.Email, u.Phone ?? "", u.Role.RoleName, u.Status]).ToList(), 0);
            ui.WriteLine(ListNav.Footer(result, "users"));
            ui.WriteLine("Type a row number (#) to open a user, or: A add user · S search · R role filter · T status filter · C clear · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");

            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, result.Pages)) continue;
            var row = ListNav.RowIndex(answer, result.Items.Count);
            if (row >= 0)
            {
                await UserDetailAsync(session, result.Items[row].UserId);
                if (session.Ended) return;
                continue;
            }
            switch (answer.ToUpperInvariant())
            {
                case "A":
                    await runner.SafeRunAsync(() => AddUserAsync(me));
                    break;
                case "S":
                    var s = ui.Ask("Search name, email or phone (Enter = no search): ");
                    filter = filter with { Search = s.Length == 0 ? null : s };
                    page = 1;
                    break;
                case "R":
                    var roles = await users.ManagedRolesAsync();
                    var role = ui.AskChoice("Role", ["All", .. roles.Select(r => r.RoleName)]);
                    filter = filter with { RoleName = role == "All" ? null : role };
                    page = 1;
                    break;
                case "T":
                    var status = ui.AskChoice("Status", ["All", "Active", "Inactive"]);
                    filter = filter with { Status = status == "All" ? null : status };
                    page = 1;
                    break;
                case "C":
                    filter = new UserFilter();
                    page = 1;
                    break;
                default:
                    ui.Error("Unknown choice. Type a row number, A, S, R, T, C, N, P or 0.");
                    break;
            }
        }
    }

    private async Task UserDetailAsync(Session session, int userId)
    {
        await runner.RunAsync(session, "User Details", async me =>
        {
            // read fresh every time the menu is shown
            var u = await users.GetAsync(userId) ?? throw new UserFacingException("This user no longer exists.");
            ui.KeyValues([
                ("User ID", u.Code ?? ""), ("Full Name", u.FullName), ("Email", u.Email), ("Phone", u.Phone ?? "-"),
                ("Role", u.Role.RoleName), ("Status", u.Status), ("Department", u.Department ?? "-"),
                ("Created On", Fmt.DateTime(u.CreatedAt)), ("Created By", u.CreatedByNavigation?.FullName ?? "-"),
                ("Last Login", Fmt.DateTime(u.LastLoginAt)), ("Must change password", u.MustChangePassword ? "Yes" : "No"),
            ]);
            return (IReadOnlyList<MenuItem>)
            [
                new("Edit user", m => EditUserAsync(m, userId)),
                new("Change role", m => ChangeRoleAsync(m, userId)),
                new("Reset password", m => ResetPasswordAsync(m, userId)),
                new(u.Status == "Active" ? "Deactivate user" : "Activate user", m => ToggleStatusAsync(m, userId)),
            ];
        }, topLevel: false);
    }

    private async Task AddUserAsync(User me)
    {
        ui.Section("Add New User");
        ui.WriteLine("Type 'cancel' at any question to go back. After the account is created you will see the login details.");
        var roles = await users.ManagedRolesAsync();
        var name = ui.AskValid("Full Name", Validation.Name);
        var email = ui.AskValid("Email", Validation.Email);
        var phone = ui.AskValid("Phone Number", Validation.Phone);
        var roleName = ui.AskChoice("Role", roles.Select(r => r.RoleName).ToList(), requiredMessage: "Select a role");
        var (created, temp) = await users.CreateAsync(name, email, phone, roles.First(r => r.RoleName == roleName).RoleId, me.UserId);
        ui.Ok($"{created.FullName} created as {created.Code}.");
        LoginDetailsBox("User Created", created, temp);
    }

    /// <summary>The "login details" popup of users.js: shown once, shared by the Super Admin in person (no email).</summary>
    private void LoginDetailsBox(string title, User u, string tempPassword)
    {
        ui.Box(title, [
            $"Name               : {u.FullName}",
            $"Role               : {u.Role.RoleName}",
            $"Email (login)      : {u.Email}",
            $"Temporary password : {tempPassword}",
            "",
            "Share these details with the user yourself. No email is sent.",
            "The password is shown only once. The user must change it at the first login.",
        ]);
    }

    private async Task EditUserAsync(User me, int userId)
    {
        var u = await users.GetAsync(userId) ?? throw new UserFacingException("This user no longer exists.");
        var roles = await users.ManagedRolesAsync();
        ui.Section($"Edit User - {u.Code}");
        ui.WriteLine("Press Enter to keep the value in [brackets]. Type 'cancel' to go back.");
        var name = ui.AskValid("Full Name", Validation.Name, u.FullName);
        var email = ui.AskValid("Email", Validation.Email, u.Email);
        var phone = ui.AskValid("Phone Number", Validation.Phone, u.Phone?.Trim() ?? "");
        var roleId = u.RoleId;
        if (u.Role.RoleCode == RoleCodes.SuperAdmin) ui.WriteLine("Role: Super Admin (a Super Admin role cannot be removed).");
        else
        {
            var roleName = ui.AskChoice("Role", roles.Select(r => r.RoleName).ToList(), u.Role.RoleName);   // ask once, then look it up
            roleId = roles.First(r => r.RoleName == roleName).RoleId;
        }
        await users.UpdateAsync(userId, name, email, phone, roleId, me.UserId);
        ui.Ok($"{name} updated");
    }

    private async Task ChangeRoleAsync(User me, int userId)
    {
        var u = await users.GetAsync(userId) ?? throw new UserFacingException("This user no longer exists.");
        if (u.Role.RoleCode == RoleCodes.SuperAdmin) throw new UserFacingException(UserAdminService.SuperAdminRoleLocked);
        var roles = await users.ManagedRolesAsync();
        ui.Section("Edit User Role");
        ui.WriteLine($"Change the role for {u.FullName}. The new permissions apply on their next login.");
        ui.WriteLine($"Current Role: {u.Role.RoleName}");
        var newRole = ui.AskChoice("New Role", roles.Select(r => r.RoleName).ToList(), u.Role.RoleName);
        var reason = ui.AskField("Reason (optional): ");
        await users.ChangeRoleAsync(userId, roles.First(r => r.RoleName == newRole).RoleId, reason, me.UserId);
        ui.Ok($"{u.FullName} is now {newRole}");
    }

    private async Task ResetPasswordAsync(User me, int userId)
    {
        var u = await users.GetAsync(userId) ?? throw new UserFacingException("This user no longer exists.");
        if (!ui.AskYesNo($"Reset the password of {u.FullName}? A new temporary password will be shown once")) return;
        var (user, temp) = await users.ResetPasswordAsync(userId, me.UserId);
        user.Role = u.Role;
        LoginDetailsBox("Password Reset", user, temp);
    }

    private async Task ToggleStatusAsync(User me, int userId)
    {
        var u = await users.GetAsync(userId) ?? throw new UserFacingException("This user no longer exists.");
        var deactivate = u.Status == "Active";
        if (deactivate && u.Role.RoleCode == RoleCodes.SuperAdmin) throw new UserFacingException(UserAdminService.SuperAdminCannotDeactivate);
        if (deactivate && u.UserId == me.UserId) throw new UserFacingException(UserAdminService.CannotDeactivateSelf);
        ui.Section(deactivate ? "Confirm Deactivation" : "Confirm Activation");
        ui.WriteLine(deactivate ? "The user will not be able to log in to the platform until activated." : "The user will be able to log in again.");
        if (!ui.AskYesNo($"Are you sure you want to {(deactivate ? "deactivate" : "activate")} {u.FullName}?")) return;
        var reason = deactivate ? ui.Ask("Reason (optional): ") : "";
        await users.SetStatusAsync(userId, !deactivate, reason, me.UserId);
        ui.Ok($"{u.FullName} {(deactivate ? "deactivated" : "activated")}");
    }

    /* ------------------------------------------------------------------ audit logs */

    private async Task AuditLogsAsync(User me)
    {
        var filter = new AuditFilter();
        var page = 1;
        while (true)
        {
            var result = await audit.QueryAsync(filter, page, PageSize);
            page = result.Page;
            ui.Section("Audit Logs (newest first)");
            var userName = filter.UserId is null ? "All" : (await audit.UsersAsync()).FirstOrDefault(u => u.UserId == filter.UserId)?.FullName ?? "?";
            ui.WriteLine($"Action: {filter.Action ?? "All"} · Performed By: {userName} · From: {Fmt.Date(filter.From)} · To: {Fmt.Date(filter.To)} · Search: {filter.Search ?? "(none)"}");
            ui.Table(["Time", "Performed By", "Action", "Target", "Details"],
                result.Items.Select(l => (IReadOnlyList<string>)[Fmt.DateTime(l.CreatedAt), l.User.FullName, l.Action, Fmt.Cut(l.Target, 32), Fmt.Cut(l.Detail, 40)]).ToList());
            ui.WriteLine(ListNav.Footer(result, "log entries"));
            ui.WriteLine("A action filter · U user filter · D date range · S search · C clear · N next · P previous · 0 back");
            var answer = ui.Ask("Choose: ");
            if (answer == "0") return;
            if (ListNav.TryPage(answer, ref page, result.Pages)) continue;
            switch (answer.ToUpperInvariant())
            {
                case "A":
                    var actions = await audit.ActionsAsync();
                    var a = ui.AskChoice("Action", ["All", .. actions]);
                    filter = filter with { Action = a == "All" ? null : a };
                    page = 1;
                    break;
                case "U":
                    var people = await audit.UsersAsync();
                    var p = ui.AskChoice("Performed By", ["All", .. people.Select(x => $"{x.FullName} ({x.Code})")]);
                    filter = filter with { UserId = p == "All" ? null : people.First(x => $"{x.FullName} ({x.Code})" == p).UserId };
                    page = 1;
                    break;
                case "D":
                    await runner.SafeRunAsync(() =>
                    {
                        ui.WriteLine("Enter the dates of the range (both days included). Enter = keep, '-' = no limit.");
                        var from = ui.AskDate("From", filter.From, allowEmpty: true);
                        var to = ui.AskDate("To", filter.To, allowEmpty: true);
                        if (from is not null && to is not null && from > to) throw new UserFacingException("From date must be before To date");
                        filter = filter with { From = from, To = to };
                        page = 1;
                        return Task.CompletedTask;
                    });
                    break;
                case "S":
                    var s = ui.Ask("Search user, action, target or details (Enter = no search): ");
                    filter = filter with { Search = s.Length == 0 ? null : s };
                    page = 1;
                    break;
                case "C":
                    filter = new AuditFilter();
                    page = 1;
                    break;
                default:
                    ui.Error("Unknown choice. Type A, U, D, S, C, N, P or 0.");
                    break;
            }
        }
    }
}
