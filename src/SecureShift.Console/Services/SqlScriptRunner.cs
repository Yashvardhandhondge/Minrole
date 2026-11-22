using Npgsql;

namespace SecureShift.ConsoleApp.Services;

/// <summary>A SQL script failed. The message says which file, which line and why.</summary>
public sealed class SqlScriptException(string message, Exception inner) : Exception(message, inner);

/// <summary>Where the .sql files are: next to the .exe in database/postgres (copied at build time).</summary>
public sealed class SqlScriptFiles(string folder)
{
    public const string CreateScript = "01_create_secureshift_in_dev.sql";
    public const string SeedScript = "02_seed_data_postgresql_dev.sql";
    public const string VerifyScript = "03_verify_dev.sql";
    public const string HistoryScript = "04_migration_history.sql";

    public static SqlScriptFiles Default() =>
        new(Path.Combine(AppContext.BaseDirectory, "database", "postgres"));

    public string Folder { get; } = folder;
    public string MigrationsFolder => Path.Combine(Folder, "migrations");

    public string PathOf(string fileName)
    {
        var path = Path.Combine(Folder, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"SQL script not found: {path}. Build the console project again (the scripts are copied at build time).", path);
        return path;
    }
}

/// <summary>Runs a whole .sql file (01, 02, 04 have their own BEGIN/COMMIT) on one connection.</summary>
public sealed class SqlScriptRunner(DbSettings settings)
{
    public async Task<IReadOnlyList<string>> RunFileAsync(string path, CancellationToken ct = default)
    {
        var sql = await File.ReadAllTextAsync(path, ct);
        var notices = new List<string>();

        // No pooling: if the script fails inside its own BEGIN, closing the connection rolls it back.
        await using var conn = new NpgsqlConnection(settings.GetAdminConnectionString());
        conn.Notice += (_, e) => notices.Add($"{e.Notice.Severity}: {e.Notice.MessageText}");
        await conn.OpenAsync(ct);

        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex)
        {
            throw new SqlScriptException(Describe(Path.GetFileName(path), sql, ex), ex);
        }

        return notices;
    }

    /// <summary>Turns a Postgres error into a message a beginner can act on.</summary>
    public static string Describe(string fileName, string sql, PostgresException ex)
    {
        var parts = new List<string> { $"{fileName} failed: {ex.MessageText} (SQLSTATE {ex.SqlState})" };

        // Npgsql sends a multi-statement script as separate statements, so Position counts
        // from the start of the failing statement. Find that statement in the file first.
        var statement = ex.BatchCommand?.CommandText?.Trim() ?? "";
        var start = statement.Length > 0 ? sql.IndexOf(statement, StringComparison.Ordinal) : -1;
        if (start >= 0)
        {
            var offset = start + Math.Max(0, ex.Position - 1);
            var line = 1 + sql.AsSpan(0, Math.Min(offset, sql.Length)).Count('\n');
            parts.Add($"  at line {line} of {fileName}");
        }
        else if (statement.Length > 0)
        {
            var firstLine = statement.Split('\n')[0].Trim();
            parts.Add($"  in the statement starting: {(firstLine.Length > 100 ? firstLine[..100] + "..." : firstLine)}");
        }
        if (!string.IsNullOrEmpty(ex.Detail)) parts.Add($"  detail: {ex.Detail}");
        if (!string.IsNullOrEmpty(ex.Hint)) parts.Add($"  hint: {ex.Hint}");
        if (!string.IsNullOrEmpty(ex.Where)) parts.Add($"  where: {ex.Where}");
        parts.Add("  Nothing from this script was saved (it runs in one transaction).");
        return string.Join(Environment.NewLine, parts);
    }
}
