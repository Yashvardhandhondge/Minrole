using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SecureShift.ConsoleApp.Services;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// Reads the command-line words and runs the matching command.
///   (no arguments)  -> the app (login screen, then the menu of the user's role)
///   db status | db setup | db seed | db migrate | db baseline   [--yes]
///   smoke
///   help
/// Exit code: 0 = success, 1 = failed / refused / cancelled, 2 = unknown command.
/// </summary>
public sealed class CommandRouter(IServiceProvider services, ConsoleUi ui)
{
    private static readonly HashSet<string> KnownFlags = ["--yes", "--help", "-h"];

    public async Task<int> RunAsync(string[] args)
    {
        var words = args.Where(a => !a.StartsWith('-')).Select(a => a.ToLowerInvariant()).ToList();
        var flags = args.Where(a => a.StartsWith('-')).Select(a => a.ToLowerInvariant()).ToHashSet();
        var yes = flags.Contains("--yes");

        var unknownFlags = flags.Where(f => !KnownFlags.Contains(f)).ToList();
        if (unknownFlags.Count > 0)
        {
            ui.Error($"Unknown option: {string.Join(" ", unknownFlags)}");
            PrintHelp();
            return 2;
        }
        if (flags.Contains("--help") || flags.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        try
        {
            // Resolved here (not in the constructor) so a missing connection string becomes a clear message below.
            DbCommands Db() => services.GetRequiredService<DbCommands>();

            return words switch
            {
                [] => await services.GetRequiredService<AppRunner>().RunAsync(),
                ["help"] => PrintHelp(),
                ["db", "status"] => await Db().StatusAsync(),
                ["db", "setup"] => await Db().SetupAsync(yes),
                ["db", "seed"] => await Db().SeedAsync(yes),
                ["db", "migrate"] => await Db().MigrateAsync(yes),
                ["db", "baseline"] => await Db().BaselineAsync(yes),
                ["smoke"] => await Db().SmokeAsync(),
                _ => Unknown(args),
            };
        }
        catch (DbConfigurationException ex)
        {
            ui.Error(ex.Message);
            return 1;
        }
        catch (SqlScriptException ex)
        {
            ui.Error(ex.Message);
            return 1;
        }
        catch (FileNotFoundException ex)
        {
            ui.Error(ex.Message);
            return 1;
        }
        catch (PostgresException ex)
        {
            ui.Error($"The database refused the request: {ex.MessageText} (SQLSTATE {ex.SqlState}).");
            return 1;
        }
        catch (InvalidOperationException ex) when (ex.InnerException is NpgsqlException or TimeoutException)
        {
            // EF Core wraps connection problems ("transient failure") in an InvalidOperationException
            ui.Error($"Could not connect to the database: {ex.InnerException!.Message}");
            ui.ErrorLine("Check the host, port, database name, user name and password in the connection string,");
            ui.ErrorLine("and that this machine can reach the database server (VPN / firewall). Tip: use 127.0.0.1 instead of localhost.");
            return 1;
        }
        catch (NpgsqlException ex)
        {
            ui.Error($"Could not connect to the database: {ex.Message}");
            ui.ErrorLine("Check the host, port, database name, user name and password in the connection string,");
            ui.ErrorLine("and that this machine can reach the database server (VPN / firewall).");
            return 1;
        }
        catch (Exception ex)
        {
            // never show a stack trace to the user
            ui.Error($"Unexpected problem ({ex.GetType().Name}): {ex.Message}");
            return 1;
        }
    }

    private int Unknown(string[] args)
    {
        ui.Error($"Unknown command: {string.Join(" ", args)}");
        PrintHelp();
        return 2;
    }

    private int PrintHelp()
    {
        ui.WriteLine("""

            SecureShift console - commands
              (no arguments)  start the app: login, then the menu of your role
              db status       show server, database, user, the 28 tables / 4 views, rows per table, migrations
              db setup        brand-new EMPTY dev schema only: runs 01 + 02 and records the baseline
              db seed         loads the 569 sample rows (only when all 28 tables are empty)
              db migrate      applies new database/postgres/migrations/V*.sql files, each in its own transaction
              db baseline     faculty DB that was set up by hand: only creates the migration history (baseline)
              smoke           counts Users / Roles / Companies / Enterprises through EF Core (expected 25 / 5 / 10 / 3)
              help            this text

            setup, seed, migrate and baseline ask "Type YES to continue" unless you add --yes.
            Example:  dotnet run --project src/SecureShift.Console -- db status
            """);
        return 0;
    }
}
