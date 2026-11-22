using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SecureShift.ConsoleApp.Services;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// Reads the command-line words and runs the matching command.
///   (no arguments)  -> the app (login menu comes in Phase 2)
///   db status | db setup | db seed | db migrate | db baseline   [--yes]
///   smoke
///   help
/// Exit code: 0 = success, 1 = failed / refused / cancelled, 2 = unknown command.
/// </summary>
public sealed class CommandRouter(IServiceProvider services)
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
            ConsoleUi.Error($"Unknown option: {string.Join(" ", unknownFlags)}");
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
                [] => RunApp(),
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
            ConsoleUi.Error(ex.Message);
            return 1;
        }
        catch (SqlScriptException ex)
        {
            ConsoleUi.Error(ex.Message);
            return 1;
        }
        catch (FileNotFoundException ex)
        {
            ConsoleUi.Error(ex.Message);
            return 1;
        }
        catch (PostgresException ex)
        {
            ConsoleUi.Error($"The database refused the request: {ex.MessageText} (SQLSTATE {ex.SqlState}).");
            return 1;
        }
        catch (NpgsqlException ex)
        {
            ConsoleUi.Error($"Could not connect to the database: {ex.Message}");
            Console.Error.WriteLine("Check the host, port, database name, user name and password in the connection string,");
            Console.Error.WriteLine("and that this machine can reach the database server (VPN / firewall).");
            return 1;
        }
    }

    private static int RunApp()
    {
        Console.WriteLine("SecureShift console");
        Console.WriteLine("Login menu comes in Phase 2.");
        Console.WriteLine("For database commands run:  dotnet run --project src/SecureShift.Console -- help");
        return 0;
    }

    private static int Unknown(string[] args)
    {
        ConsoleUi.Error($"Unknown command: {string.Join(" ", args)}");
        PrintHelp();
        return 2;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""

            SecureShift console - commands
              (no arguments)  start the app (login menu comes in Phase 2)
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
