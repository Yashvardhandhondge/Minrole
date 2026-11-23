using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Context.Generated;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Services;

/// <summary>
/// Who is logged in. Only the user id, the role code at login time and the login time are kept:
/// every menu action reads the user again from the database (<see cref="AuthService.CheckSessionAsync"/>).
/// </summary>
public sealed class Session(int userId, string roleCode, DateTime loginAtUtc)
{
    public int UserId { get; } = userId;
    public string RoleCode { get; } = roleCode;
    public DateTime LoginAtUtc { get; } = loginAtUtc;

    /// <summary>Admin / Architect work inside one enterprise at a time (SS.EM.entId in the prototype).</summary>
    public int? EnterpriseId { get; set; }

    public bool Ended { get; private set; }
    public string? EndMessage { get; private set; }

    public void End(string? message)
    {
        Ended = true;
        EndMessage = message;
    }
}

public enum LoginStatus { Success, MissingInput, BadEmailFormat, InvalidCredentials, Inactive }

/// <summary>Result of a login attempt (not an entity copy: the status, the message to show and the user id).</summary>
public sealed record LoginResult(LoginStatus Status, string Message, int? UserId = null, string? RoleCode = null, bool MustChangePassword = false, bool HashUpgraded = false);

/// <summary>Login, logout, password change and the per-action session check.</summary>
public sealed class AuthService(IDbContextFactory<SecureShiftDbContext> factory, TimeProvider clock, PasswordService passwords)
    : ServiceBase(factory, clock)
{
    // login.js messages
    public const string MissingInputMessage = "Please enter your email and password.";
    public const string BadEmailMessage = "Please enter a valid email address.";
    public const string InvalidMessage = "Invalid email or password.";
    public const string InactiveMessage = "Your account is inactive. Please contact your Super Admin.";

    public const string DeactivatedMidSession = "Your account is no longer active. You have been logged out.";
    public const string RoleChangedMidSession = "Your role was changed. Please log in again.";

    public async Task<LoginResult> LoginAsync(string? emailInput, string? password)
    {
        var email = (emailInput ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0 || string.IsNullOrEmpty(password)) return new(LoginStatus.MissingInput, MissingInputMessage);
        if (!Validation.IsLoginEmail(email)) return new(LoginStatus.BadEmailFormat, BadEmailMessage);

        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        // Email is compared case-insensitively (the prototype lower-cases what was typed).
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user is null) return new(LoginStatus.InvalidCredentials, InvalidMessage);   // nothing to audit: no user id

        var check = passwords.Verify(user, password);
        var now = UtcNow;
        if (check == PasswordCheck.Failed)
        {
            Audit.Add(db, user.UserId, "Login Failed", user.FullName, "Wrong password", now);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return new(LoginStatus.InvalidCredentials, InvalidMessage);   // same message: does not reveal that the email exists
        }

        if (user.Status != "Active")
        {
            Audit.Add(db, user.UserId, "Login Blocked", user.FullName, "Inactive account", now);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return new(LoginStatus.Inactive, InactiveMessage);
        }

        var upgraded = check == PasswordCheck.SuccessUpgrade;
        if (upgraded) user.PasswordHash = passwords.Hash(user, password);   // legacy SHA-256 -> Identity V3
        user.LastLoginAt = now;
        Audit.Add(db, user.UserId, "Logged In", user.FullName, upgraded ? "Console session · password hash upgraded to V3" : "Console session", now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return new(LoginStatus.Success, "", user.UserId, user.Role.RoleCode, user.MustChangePassword, upgraded);
    }

    public async Task LogoutAsync(Session session)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == session.UserId);
        if (user is not null)
        {
            Audit.Add(db, user.UserId, "Logged Out", user.FullName, "Console session", UtcNow);
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
    }

    /// <summary>
    /// Fresh read of the logged-in user before every menu action. Returns null (and ends the session)
    /// when the account was deactivated / deleted or the role was changed since login.
    /// </summary>
    public async Task<User?> CheckSessionAsync(Session session)
    {
        await using var db = await NewDbAsync();
        var user = await db.Users.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == session.UserId);
        if (user is null || user.Status != "Active")
        {
            session.End(DeactivatedMidSession);
            return null;
        }
        if (user.Role.RoleCode != session.RoleCode)
        {
            session.End(RoleChangedMidSession);
            return null;
        }
        return user;
    }

    /// <summary>profile.js passwordRules + "new must be different from current". Throws UserFacingException with the field messages.</summary>
    public async Task ChangePasswordAsync(int userId, string current, string newPassword, string confirm, bool forced)
    {
        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId) ?? throw new UserFacingException("User not found.");

        var currentOk = passwords.Verify(user, current) != PasswordCheck.Failed;
        Validation.ThrowIfAny(
            ("Current password", currentOk ? "" : "Current password is wrong"),
            ("New password", Validation.Password(newPassword)),
            ("Confirm", !string.IsNullOrEmpty(confirm) && confirm == newPassword ? "" : "Passwords do not match"),
            ("New password", currentOk && newPassword == current ? "New password must be different from the current password" : ""));

        user.PasswordHash = passwords.Hash(user, newPassword);
        user.MustChangePassword = false;
        Audit.Add(db, user.UserId, "Password Changed", user.FullName, forced ? "Temporary password replaced at first login" : "Changed on the Profile page", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Profile > Account Details (name, phone, department) with the profile.js rules.</summary>
    public async Task UpdateProfileAsync(int userId, string fullName, string phone, string department)
    {
        fullName = fullName.Trim();
        phone = phone.Trim();
        department = department.Trim();
        Validation.ThrowIfAny(
            ("Name", Validation.Name(fullName)),
            ("Phone", Validation.Phone(phone)),
            ("Department", Validation.Dept(department)));

        await using var db = await NewDbAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId) ?? throw new UserFacingException("User not found.");
        user.FullName = fullName;
        user.Phone = phone;
        user.Department = department;
        Audit.Add(db, user.UserId, "Profile Updated", user.FullName, "Name, phone and department", UtcNow);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>The user's own audit rows (Profile > Activity in the prototype).</summary>
    public async Task<List<AuditLog>> MyActivityAsync(int userId, int take)
    {
        await using var db = await NewDbAsync();
        return await db.AuditLogs.AsNoTracking().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditLogId).Take(take).ToListAsync();
    }

    public async Task<User?> GetUserAsync(int userId)
    {
        await using var db = await NewDbAsync();
        return await db.Users.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
    }
}
