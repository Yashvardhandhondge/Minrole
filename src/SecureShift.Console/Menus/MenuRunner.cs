using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecureShift.ConsoleApp.Services;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>One numbered menu line and what it does. The action gets the logged-in user, read fresh from the DB.</summary>
public sealed record MenuItem(string Label, Func<User, Task> Action);

/// <summary>
/// Runs a numbered menu: 1..n = items, 0 = Back (or Logout on the top menu).
/// Before showing the menu AND before running the chosen item the logged-in user is read again from the
/// database: a user who was deactivated (or whose role was changed) in the meantime is logged out at once.
/// Any error inside an item is shown as [ERROR] and the menu comes back (the app never crashes on bad input).
/// </summary>
public sealed class MenuRunner(ConsoleUi ui, AuthService auth)
{
    public Task RunAsync(Session session, string title, Func<User, IReadOnlyList<MenuItem>> items, bool topLevel) =>
        RunAsync(session, title, me => Task.FromResult(items(me)), topLevel);

    /// <summary>Same, but the menu lines are built with a fresh (async) database read each time the menu is shown.</summary>
    public async Task RunAsync(Session session, string title, Func<User, Task<IReadOnlyList<MenuItem>>> items, bool topLevel)
    {
        while (true)
        {
            var me = await auth.CheckSessionAsync(session);
            if (me is null) return;

            ui.Title(title);
            if (topLevel) ui.WriteLine($"Logged in as {me.FullName} ({me.Role.RoleName}) · {RoleCodes.ProductTitle(me.Role.Product)}");
            IReadOnlyList<MenuItem> list;
            try
            {
                list = await items(me);
            }
            catch (UserFacingException ex)
            {
                ui.Error(ex.Message);
                return;
            }
            for (var i = 0; i < list.Count; i++) ui.WriteLine($"  {i + 1}. {list[i].Label}");
            ui.WriteLine(topLevel ? "  0. Logout" : "  0. Back");

            var answer = ui.Ask("Choose: ");
            if (answer == "0")
            {
                if (!topLevel) return;
                if (await ConfirmLogoutAsync(session, me)) return;
                continue;
            }
            if (!int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1 || n > list.Count)
            {
                ui.Error($"Type a number from 0 to {list.Count}.");
                continue;
            }

            me = await auth.CheckSessionAsync(session);   // fresh check right before the action
            if (me is null) return;
            await SafeRunAsync(() => list[n - 1].Action(me));
            if (session.Ended) return;
        }
    }

    /// <summary>logout.html: "Are you sure you want to logout ...?" then the audit row "Logged Out".</summary>
    public async Task<bool> ConfirmLogoutAsync(Session session, User me)
    {
        if (!ui.AskYesNo($"Are you sure you want to logout, {me.FullName}?")) return false;
        await auth.LogoutAsync(session);
        session.End(null);
        ui.Ok("You have been logged out. Thank you for using SecureShift.");
        return true;
    }

    /// <summary>Runs an action and turns every expected problem into a clear message.</summary>
    public async Task SafeRunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (FormCancelledException)
        {
            ui.Info("Cancelled. Nothing was saved.");
        }
        catch (UserFacingException ex)
        {
            ui.Error(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            ui.Error("Someone else changed or deleted this record in the meantime. Nothing was saved. Open it again to see the latest data.");
        }
        catch (DbUpdateException ex)
        {
            var pg = ex.InnerException as PostgresException;
            ui.Error("The database refused the change: " + (pg?.MessageText ?? ex.InnerException?.Message ?? ex.Message)
                     + (pg?.ConstraintName is { } c ? $" (rule {c})" : "") + ". Nothing was saved.");
        }
        catch (PostgresException ex)
        {
            ui.Error($"The database refused the request: {ex.MessageText} (SQLSTATE {ex.SqlState}). Nothing was saved.");
        }
        catch (NpgsqlException ex)
        {
            ui.Error($"Could not reach the database: {ex.Message}. Check the connection and try again.");
        }
        catch (TimeoutException ex)
        {
            ui.Error($"The database did not answer in time: {ex.Message}");
        }
        catch (InvalidOperationException ex) when (ex.InnerException is NpgsqlException or TimeoutException)
        {
            ui.Error($"Could not reach the database: {ex.InnerException!.Message}. Check the connection and try again.");
        }
        catch (InputEndedException)
        {
            throw;   // handled by AppRunner: clean exit
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // never crash, never show a stack trace
            ui.Error($"Unexpected problem ({ex.GetType().Name}): {ex.Message}. Nothing was saved.");
        }
    }
}

/// <summary>Paging and row picking for list screens ("N next, P previous, number = open").</summary>
public static class ListNav
{
    /// <summary>Handles N / P. Returns true when the answer was a paging command.</summary>
    public static bool TryPage(string answer, ref int page, int pages)
    {
        if (answer.Equals("n", StringComparison.OrdinalIgnoreCase))
        {
            page = Math.Min(pages, page + 1);
            return true;
        }
        if (answer.Equals("p", StringComparison.OrdinalIgnoreCase))
        {
            page = Math.Max(1, page - 1);
            return true;
        }
        return false;
    }

    /// <summary>A row number of the current page (1-based) → index, or -1.</summary>
    public static int RowIndex(string answer, int rowsOnPage) =>
        int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= rowsOnPage ? n - 1 : -1;

    public static string Footer<T>(Paged<T> p, string unit) =>
        p.Total == 0 ? $"No {unit} found." : $"Showing {p.FirstNumber}-{p.LastNumber} of {p.Total} {unit} · Page {p.Page} of {p.Pages}";
}
