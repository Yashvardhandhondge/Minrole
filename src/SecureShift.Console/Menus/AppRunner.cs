using Microsoft.Extensions.DependencyInjection;
using SecureShift.ConsoleApp.Services;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// The interactive app: login screen → (forced password change) → the menu of the user's role → logout → login screen.
/// Each role only ever sees its own menu.
/// </summary>
public sealed class AppRunner(ConsoleUi ui, AuthService auth, MenuRunner runner, IServiceProvider services, TimeProvider clock)
{
    public async Task<int> RunAsync()
    {
        ui.ErrorsToStdErr = false;   // inside the app errors are shown on the same screen as everything else
        try
        {
            while (true)
            {
                ui.Title("SECURESHIFT · Modernize · Govern · Secure");
                ui.WriteLine("Welcome Back. Sign in to access your SecureShift account.");
                ui.WriteLine("  1. Log in");
                ui.WriteLine("  2. Forgot password?");
                ui.WriteLine("  0. Exit");
                var choice = ui.Ask("Choose: ");
                switch (choice)
                {
                    case "1":
                        await runner.SafeRunAsync(LoginAsync);
                        break;
                    case "2":
                        // login.js: no email is sent, the Super Admin gives a temporary password manually
                        ui.Section("Forgot Password");
                        ui.WriteLine("Please contact your Super Admin.");
                        ui.WriteLine("They will reset your password in User Management and give you a temporary password directly.");
                        ui.WriteLine("Change it on the Profile page after you log in.");
                        break;
                    case "0":
                        ui.WriteLine("Goodbye.");
                        return 0;
                    default:
                        ui.Error("Type 1, 2 or 0.");
                        break;
                }
            }
        }
        catch (InputEndedException)
        {
            ui.WriteLine();
            ui.WriteLine("Input ended. Goodbye.");
            return 0;
        }
    }

    private async Task LoginAsync()
    {
        ui.Section("Login");
        ui.WriteLine("User accounts are managed by SecureShift Administrators.");
        var email = ui.Ask("Email: ");
        var password = ui.AskPassword("Password: ");
        var result = await auth.LoginAsync(email, password);
        if (result.Status != LoginStatus.Success)
        {
            ui.Error(result.Message);
            return;
        }

        var session = new Session(result.UserId!.Value, result.RoleCode!, clock.GetUtcNow().UtcDateTime);
        if (result.HashUpgraded) ui.Info("Your password was stored in an old format; it has been upgraded to the new secure format.");

        if (result.MustChangePassword && !await ForcedPasswordChangeAsync(session)) return;

        var me = await auth.CheckSessionAsync(session);
        if (me is null)
        {
            ui.Warn(session.EndMessage ?? "Your session ended.");
            return;
        }
        ui.Ok($"Signed in as {me.FullName} ({me.Role.RoleName}).");

        switch (session.RoleCode)
        {
            case RoleCodes.SuperAdmin: await services.GetRequiredService<SuperAdminMenu>().RunAsync(session); break;
            case RoleCodes.CloudAdmin: await services.GetRequiredService<CloudAdminMenu>().RunAsync(session); break;
            case RoleCodes.Admin: await services.GetRequiredService<AdminMenu>().RunAsync(session); break;
            case RoleCodes.Architect: await services.GetRequiredService<ArchitectMenu>().RunAsync(session); break;
            case RoleCodes.Executive: await services.GetRequiredService<ExecutiveMenu>().RunAsync(session); break;
            default: ui.Error($"Unknown role '{session.RoleCode}'. Ask the Super Admin to fix your account."); break;
        }
        if (session.EndMessage is not null) ui.Warn(session.EndMessage);
    }

    /// <summary>MustChangePassword = true (new user / password reset): the temporary password must be replaced first.</summary>
    private async Task<bool> ForcedPasswordChangeAsync(Session session)
    {
        ui.Section("Change your password");
        ui.WriteLine("You are using a temporary password. Choose a new password before you continue.");
        ui.WriteLine("Minimum 8 characters with an uppercase letter, a lowercase letter, a number and a special character.");
        ui.WriteLine("(Type 'cancel' as the current password to log out.)");
        while (true)
        {
            var current = ui.AskPassword("Current (temporary) password: ");
            if (current.Equals(ConsoleUi.CancelWord, StringComparison.OrdinalIgnoreCase))
            {
                await auth.LogoutAsync(session);
                ui.Info("Logged out. Your password was not changed.");
                return false;
            }
            var newPassword = ui.AskPassword("New password: ");
            var confirm = ui.AskPassword("Confirm new password: ");
            try
            {
                await auth.ChangePasswordAsync(session.UserId, current, newPassword, confirm, forced: true);
                ui.Ok("Password updated. Use the new password next time you log in.");
                return true;
            }
            catch (UserFacingException ex)
            {
                ui.Error(ex.Message);
            }
        }
    }
}
