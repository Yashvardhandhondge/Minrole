using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SecureShift.ConsoleApp.Hosting;
using SecureShift.ConsoleApp.Menus;
using SecureShift.ConsoleApp.Services;
using SecureShift.Data.Context.Generated;

namespace SecureShift.Tests;

/// <summary>
/// A brand-new database for tests that WRITE data: CREATE DATABASE, CREATE SCHEMA dev, then 01 + 02 + 04
/// (exactly what "db setup" does). Each test class that uses it gets its own copy, so writes never touch
/// the shared seeded database that the read-only tests check (569 rows). Dropped again at the end.
/// </summary>
public sealed class FreshDatabase : IAsyncLifetime
{
    public string Name { get; } = "ss_t_" + Guid.NewGuid().ToString("N")[..12];
    public string ConnectionString { get; private set; } = "";
    private string adminConnectionString = "";

    public async Task InitializeAsync()
    {
        var main = new DbSettings(Environment.GetEnvironmentVariable(DbSettings.EnvironmentVariable));
        if (!main.IsConfigured)
            throw new InvalidOperationException($"Set {DbSettings.EnvironmentVariable} (a PostgreSQL user that may CREATE DATABASE) to run the database tests.");

        var admin = new NpgsqlConnectionStringBuilder(main.GetConnectionString()) { Database = "postgres", Pooling = false };
        adminConnectionString = admin.ConnectionString;
        await ExecAsync(adminConnectionString, $"CREATE DATABASE \"{Name}\"");

        ConnectionString = new NpgsqlConnectionStringBuilder(main.GetConnectionString()) { Database = Name }.ConnectionString;
        var settings = new DbSettings(ConnectionString);
        await ExecAsync(settings.GetAdminConnectionString(), "CREATE SCHEMA dev");
        var runner = new SqlScriptRunner(settings);
        var files = SqlScriptFiles.Default();
        foreach (var f in new[] { SqlScriptFiles.CreateScript, SqlScriptFiles.SeedScript, SqlScriptFiles.HistoryScript })
            await runner.RunFileAsync(files.PathOf(f));
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecAsync(adminConnectionString, $"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)");
    }

    /// <summary>Runs SQL directly (like a teammate using SQLTools / psql while the app is open).</summary>
    public Task SqlAsync(string sql) => ExecAsync(ConnectionString, sql);

    public async Task<object?> ScalarAsync(string sql)
    {
        await using var c = new NpgsqlConnection(ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task ExecAsync(string conn, string sql)
    {
        await using var c = new NpgsqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    public SecureShiftDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<SecureShiftDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>The app's real DI setup, pointed at this database, with scripted console input.</summary>
    public ServiceProvider BuildServices(IConsoleIO? io = null, TimeProvider? clock = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:" + DbSettings.ConnectionStringName] = ConnectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(io ?? new ScriptedConsoleIO());
        AppHost.AddSecureShift(services, config);
        if (clock is not null) services.AddSingleton(clock);
        return services.BuildServiceProvider();
    }

    public T Get<T>() where T : notnull => BuildServices().GetRequiredService<T>();

    /// <summary>Runs the interactive app with these input lines and returns everything it printed.</summary>
    public Task<(int ExitCode, string Output)> RunAppAsync(params string[] lines) => RunAppAsync((IEnumerable<object>)lines);

    public async Task<(int ExitCode, string Output)> RunAppAsync(IEnumerable<object> steps)
    {
        var io = new ScriptedConsoleIO(steps);
        await using var sp = BuildServices(io);
        var code = await sp.GetRequiredService<AppRunner>().RunAsync();
        return (code, io.Output);
    }
}

/// <summary>
/// A fake terminal: input comes from a list of lines, output is collected in a string.
/// An input step can also be a Func&lt;string&gt;: it runs when the app asks for that line (e.g. to change the
/// database in the middle of a session, like a teammate in SQLTools) and returns the line to type.
/// </summary>
public sealed class ScriptedConsoleIO : IConsoleIO
{
    private readonly Queue<object> input;
    private readonly StringBuilder output = new();

    public ScriptedConsoleIO(params string[] lines) => input = new Queue<object>(lines);
    public ScriptedConsoleIO(IEnumerable<object> steps) => input = new Queue<object>(steps);

    private string Next() => input.Dequeue() switch
    {
        string s => s,
        Func<string> f => f(),
        var other => throw new InvalidOperationException($"Bad script step {other}"),
    };

    public string Output => output.ToString();
    public int Remaining => input.Count;

    public void Write(string text) => output.Append(text);
    public void WriteLine(string text = "") => output.Append(text).Append('\n');
    public void WriteColored(string text, ConsoleColor color) => output.Append(text);
    public void WriteErrorLine(string text) => output.Append(text).Append('\n');
    public void Clear() { }

    public string? ReadLine()
    {
        if (input.Count == 0) return null;
        var line = Next();
        output.Append(line).Append('\n');   // echo, so the output reads like a terminal session
        return line;
    }

    public string? ReadPassword()
    {
        if (input.Count == 0) return null;
        var line = Next();
        output.Append(new string('*', line.Length)).Append('\n');
        return line;
    }
}

/// <summary>A clock that always says the same time (for date-based rules).</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
