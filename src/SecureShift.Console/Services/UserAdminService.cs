using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Context.Generated;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Services;

/// <summary>Filters of the Super Admin user list (search text, role name, status). Not an entity.</summary>
public sealed record UserFilter(string? Search = null, string? RoleName = null, string? Status = null);

/// <summary>Numbers of the Super Admin dashboard (not an entity).</summary>
public sealed record SuperAdminDashboard(int Total, int Active, int Inactive, IReadOnlyList<(string Role, int Count)> ByRole,
    IReadOnlyList<AuditLog> RecentActivity, IReadOnlyList<AuditLog> AccessChanges);

/// <summary>
/// Super Admin > User Management (modernization/js/users.js) and the dashboard numbers
/// (modernization/js/superadmin-dashboard.js). Only Modernization users are managed here,
/// like the prototype (its ROLES list is Super Admin, Admin, Architect, Executive).
/// </summary>
public sealed partial class UserAdminService(IDbContextFactory<SecureShiftDbContext> factory, TimeProvider clock, PasswordService passwords)
    : ServiceBase(factory, clock)
{
    public const string EmailExistsMessage = "Email already exists";
    public const string SuperAdminRoleLocked = "A Super Admin role cannot be removed or changed";
    public const string SuperAdminCannotDeactivate = "A Super Admin cannot be deactivated or removed";
    public const string CannotDeactivateSelf = "You cannot deactivate your own account";

    [GeneratedRegex("Role|Created|Deactivated|Activated|Password")] private static partial Regex AccessChangeRx();

    private static IQueryable<User> Managed(SecureShiftDbContext db) =>
        db.Users.Include(u => u.Role).Where(u => u.Role.Product == RoleCodes.ProductModernization);

    /// <summary>The 4 roles a Super Admin can give (order of the prototype's ROLES list).</summary>
    public async Task<List<Role>> ManagedRolesAsync()
    {
        await using var db = await NewDbAsync();
        return await db.Roles.AsNoTracking().Where(r => r.Product == RoleCodes.ProductModernization).OrderBy(r => r.RoleId).ToListAsync();
    }

    public async Task<Paged<User>> ListAsync(UserFilter filter, int page, int pageSize = 10)
    {
        await using var db = await NewDbAsync();
        var q = Managed(db).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            q = q.Where(u => (u.FullName + " " + u.Email + " " + (u.Phone ?? "")).ToLower().Contains(s));
        }
        if (!string.IsNullOrEmpty(filter.RoleName)) q = q.Where(u => u.Role.RoleName == filter.RoleName);
        if (!string.IsNullOrEmpty(filter.Status)) q = q.Where(u => u.Status == filter.Status);
        return await Paged<User>.FromQueryAsync(q.OrderBy(u => u.UserId), page, pageSize);
    }

    public async Task<User?> GetAsync(int userId)
    {
        await using var db = await NewDbAsync();
        return await db.Users.AsNoTracking().Include(u => u.Role).Include(u => u.CreatedByNavigation)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<User?> FindByCodeAsync(string code)
    {
        await using var db = await NewDbAsync();
        var c = code.Trim().ToUpperInvariant();
        return await Managed(db).AsNoTracking().FirstOrDefaultAsync(u => u.Code == c);
    }

    public async Task<SuperAdminDashboard> DashboardAsync()
    {
        await using var db = await NewDbAsync();
        var users = await Managed(db).AsNoTracking().ToListAsync();
        var roles = await db.Roles.AsNoTracking().Where(r => r.Product == RoleCodes.ProductModernization).OrderBy(r => r.RoleId).ToListAsync();
        var byRole = roles.Select(r => (r.RoleName, users.Count(u => u.RoleId == r.RoleId))).ToList();
        var logs = await db.AuditLogs.AsNoTracking().Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditLogId).Take(200).ToListAsync();
        var active = users.Count(u => u.Status == "Active");
        return new SuperAdminDashboard(users.Count, active, users.Count - active, byRole,
            logs.Take(5).ToList(), logs.Where(l => AccessChangeRx().IsMatch(l.Action)).Take(6).ToList());
    }

    private static async Task CheckEmailFreeAsync(SecureShiftDbContext db, string email, int? exceptUserId)
    {
        var lower = email.ToLower();
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == lower && u.UserId != (exceptUserId ?? 0)))
            throw new UserFacingException($"Email: {EmailExistsMessage}");
    }

    private static async Task<Role> ManagedRoleAsync(SecureShiftDbContext db, int roleId) =>
        await db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId && r.Product == RoleCodes.ProductModernization)
        ?? throw new UserFacingException("Role: Select a role");

    /// <summary>Add New User. New users are Active and must change the temporary password at first login.</summary>
    public async Task<(User User, string TemporaryPassword)> CreateAsync(string fullName, string email, string phone, int? roleId, int actorId)
    {
        fullName = fullName.Trim();
        email = email.Trim();
        phone = phone.Trim();
        Validation.ThrowIfAny(
            ("Name", Validation.Name(fullName)),
            ("Email", Validation.Email(email)),
            ("Phone", Validation.Phone(phone)),
            ("Role", roleId is null ? "Select a role" : ""));

        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await CheckEmailFreeAsync(db, email, null);
        var role = await ManagedRoleAsync(db, roleId!.Value);

        var temp = PasswordService.NewTemporaryPassword();
        var now = UtcNow;
        var user = new User
        {
            FullName = fullName,
            Email = email,
            Phone = phone,
            RoleId = role.RoleId,
            Status = "Active",
            MustChangePassword = true,
            CreatedAt = now,
            CreatedBy = actorId,
            PasswordHash = "",
        };
        user.PasswordHash = passwords.Hash(user, temp);
        db.Users.Add(user);
        Audit.Add(db, actorId, "User Created", fullName, "Role: " + role.RoleName, now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        user.Role = role;
        return (user, temp);
    }

    /// <summary>Edit User (name, email, phone, role). A Super Admin keeps the Super Admin role.</summary>
    public async Task UpdateAsync(int userId, string fullName, string email, string phone, int roleId, int actorId)
    {
        fullName = fullName.Trim();
        email = email.Trim();
        phone = phone.Trim();
        Validation.ThrowIfAny(
            ("Name", Validation.Name(fullName)),
            ("Email", Validation.Email(email)),
            ("Phone", Validation.Phone(phone)));

        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await Managed(db).FirstOrDefaultAsync(u => u.UserId == userId) ?? throw new UserFacingException("User not found.");
        await CheckEmailFreeAsync(db, email, userId);
        var oldRole = user.Role;
        var newRole = oldRole.RoleCode == RoleCodes.SuperAdmin ? oldRole : await ManagedRoleAsync(db, roleId);   // Super Admin can never be demoted

        var now = UtcNow;
        var oldName = user.FullName;
        if (newRole.RoleId != oldRole.RoleId)
        {
            Audit.Add(db, actorId, "Role Changed", oldName, oldRole.RoleName + " → " + newRole.RoleName, now);
            user.RoleId = newRole.RoleId;
        }
        user.FullName = fullName;
        user.Email = email;
        user.Phone = phone;
        Audit.Add(db, actorId, "User Updated", fullName, "Details edited", now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Edit User Role (the "Change Role" action).</summary>
    public async Task ChangeRoleAsync(int userId, int roleId, string? reason, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await Managed(db).FirstOrDefaultAsync(u => u.UserId == userId) ?? throw new UserFacingException("User not found.");
        if (user.Role.RoleCode == RoleCodes.SuperAdmin) throw new UserFacingException(SuperAdminRoleLocked);
        var newRole = await ManagedRoleAsync(db, roleId);
        if (newRole.RoleId == user.RoleId) throw new UserFacingException("Role is unchanged");

        var detail = user.Role.RoleName + " → " + newRole.RoleName + (string.IsNullOrWhiteSpace(reason) ? "" : " · " + reason.Trim());
        Audit.Add(db, actorId, "Role Changed", user.FullName, detail, UtcNow);
        user.RoleId = newRole.RoleId;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Activate / Deactivate. A Super Admin can never be deactivated (so there is always an active Super Admin).</summary>
    public async Task SetStatusAsync(int userId, bool active, string? reason, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await Managed(db).FirstOrDefaultAsync(u => u.UserId == userId) ?? throw new UserFacingException("User not found.");
        if (!active)
        {
            if (user.UserId == actorId) throw new UserFacingException(CannotDeactivateSelf);
            if (user.Role.RoleCode == RoleCodes.SuperAdmin) throw new UserFacingException(SuperAdminCannotDeactivate);
        }
        var newStatus = active ? "Active" : "Inactive";
        if (user.Status == newStatus) throw new UserFacingException($"{user.FullName} is already {newStatus}.");

        user.Status = newStatus;
        Audit.Add(db, actorId, active ? "Account Activated" : "Account Deactivated", user.FullName, (reason ?? "").Trim(), UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Reset Password: a new temporary password, shown once on screen (no email is sent).</summary>
    public async Task<(User User, string TemporaryPassword)> ResetPasswordAsync(int userId, int actorId)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await Managed(db).FirstOrDefaultAsync(u => u.UserId == userId) ?? throw new UserFacingException("User not found.");
        var temp = PasswordService.NewTemporaryPassword();
        user.PasswordHash = passwords.Hash(user, temp);
        user.MustChangePassword = true;
        Audit.Add(db, actorId, "Password Reset", user.FullName, "Temporary password given manually", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (user, temp);
    }
}
