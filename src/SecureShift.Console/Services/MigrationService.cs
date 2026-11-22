using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace SecureShift.ConsoleApp.Services;

/// <summary>One file in database/postgres/migrations, e.g. V002__add_user_nickname.sql.</summary>
public sealed record MigrationFile(int Version, string Name, string FileName, string FullPath, string Checksum);

/// <summary>One row of dev."__SchemaMigrations".</summary>
public sealed record AppliedMigration(int Version, string Name, string ScriptName, string? Checksum, DateTime AppliedAtUtc, string AppliedBy);

/// <summary>What db migrate would do.</summary>
public sealed record MigrationPlan(
    IReadOnlyList<AppliedMigration> Applied,
    IReadOnlyList<MigrationFile> Pending,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Applies database/postgres/migrations/V*.sql in version order. Each file runs in its own
/// transaction together with its row in dev."__SchemaMigrations": all or nothing.
/// </summary>
public sealed partial class MigrationService(DbSettings settings, SqlScriptFiles files)
{
    public const int BaselineVersion = 1;

    [GeneratedRegex(@"^V(?<version>\d{3,})__(?<name>[A-Za-z0-9_]+)\.sql$")]
    private static partial Regex FileNameRegex();

    // A migration must not manage its own transaction (the console does it).
    [GeneratedRegex(@"^\s*(BEGIN\s*(TRANSACTION|WORK)?\s*;|START\s+TRANSACTION\b|COMMIT\s*(TRANSACTION|WORK)?\s*;|ROLLBACK\s*;|END\s+(TRANSACTION|WORK)\s*;)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex TransactionStatementRegex();

    public static bool TryParseFileName(string fileName, out int version, out string name)
    {
        var m = FileNameRegex().Match(fileName);
        version = m.Success ? int.Parse(m.Groups["version"].Value) : 0;
        name = m.Success ? m.Groups["name"].Value : "";
        return m.Success;
    }

    /// <summary>SHA-256 of the file text with CRLF/CR changed to LF and no BOM, so Windows and Linux agree.</summary>
    public static string ComputeChecksum(string text)
    {
        var normalized = text.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    public static bool ContainsTransactionStatements(string sql) => TransactionStatementRegex().IsMatch(sql);

    /// <summary>Reads the migration files. Bad names, duplicates and BEGIN/COMMIT are reported as problems.</summary>
    public (IReadOnlyList<MigrationFile> Files, IReadOnlyList<string> Problems) LoadFiles()
    {
        var result = new List<MigrationFile>();
        var problems = new List<string>();
        if (!Directory.Exists(files.MigrationsFolder)) return (result, problems);

        foreach (var path in Directory.GetFiles(files.MigrationsFolder, "*.sql").OrderBy(p => p, StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(path);
            if (!TryParseFileName(fileName, out var version, out var name))
            {
                problems.Add($"{fileName}: bad file name. Use V002__short_name.sql (V + 3 digits + two underscores + letters/digits/_).");
                continue;
            }
            if (version <= BaselineVersion)
            {
                problems.Add($"{fileName}: version {version} is reserved for the baseline. Start at V002.");
                continue;
            }
            var text = File.ReadAllText(path);
            if (ContainsTransactionStatements(text))
            {
                problems.Add($"{fileName}: remove BEGIN/COMMIT/ROLLBACK. The console runs every migration in its own transaction.");
                continue;
            }
            result.Add(new MigrationFile(version, name, fileName, path, ComputeChecksum(text)));
        }

        foreach (var dup in result.GroupBy(f => f.Version).Where(g => g.Count() > 1))
            problems.Add($"Version {dup.Key} is used by more than one file: {string.Join(", ", dup.Select(f => f.FileName))}.");

        return (result.OrderBy(f => f.Version).ToList(), problems);
    }

    public static async Task<bool> HistoryTableExistsAsync(NpgsqlConnection conn, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", conn);
        cmd.Parameters.AddWithValue("name", SchemaInfo.Qualified(SchemaInfo.MigrationsTable));
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public static async Task<IReadOnlyList<AppliedMigration>> GetAppliedAsync(NpgsqlConnection conn, CancellationToken ct = default)
    {
        var list = new List<AppliedMigration>();
        if (!await HistoryTableExistsAsync(conn, ct)) return list;

        await using var cmd = new NpgsqlCommand(
            $"""SELECT "Version", "Name", "ScriptName", "Checksum", "AppliedAt", "AppliedBy" FROM {SchemaInfo.Qualified(SchemaInfo.MigrationsTable)} ORDER BY "Version" """, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new AppliedMigration(
                r.GetInt32(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3).Trim(),
                r.GetDateTime(4), r.GetString(5)));
        }
        return list;
    }

    public async Task<MigrationPlan> PlanAsync(NpgsqlConnection conn, CancellationToken ct = default)
    {
        var (all, problems) = LoadFiles();
        var applied = await GetAppliedAsync(conn, ct);
        var appliedByVersion = applied.ToDictionary(a => a.Version);
        var warnings = new List<string>();

        foreach (var f in all.Where(f => appliedByVersion.ContainsKey(f.Version)))
        {
            var a = appliedByVersion[f.Version];
            if (a.Checksum is not null && !string.Equals(a.Checksum, f.Checksum, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"{f.FileName} was changed after it was applied on {a.AppliedAtUtc:yyyy-MM-dd}. Do not edit applied migrations; add a new V file instead.");
        }

        var pending = all.Where(f => !appliedByVersion.ContainsKey(f.Version)).ToList();
        var highestApplied = applied.Count == 0 ? 0 : applied.Max(a => a.Version);
        foreach (var f in pending.Where(f => f.Version < highestApplied))
            warnings.Add($"{f.FileName} has a lower version than the newest applied migration (V{highestApplied:000}); it will still be applied.");

        return new MigrationPlan(applied, pending, problems, warnings);
    }

    /// <summary>Runs one migration file and records it, in ONE transaction.</summary>
    public async Task ApplyAsync(MigrationFile file, CancellationToken ct = default)
    {
        var sql = await File.ReadAllTextAsync(file.FullPath, ct);
        await using var conn = new NpgsqlConnection(settings.GetAdminConnectionString());
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = new NpgsqlCommand(sql, conn, tx))
                await cmd.ExecuteNonQueryAsync(ct);

            await using (var record = new NpgsqlCommand(
                $"""INSERT INTO {SchemaInfo.Qualified(SchemaInfo.MigrationsTable)} ("Version", "Name", "ScriptName", "Checksum") VALUES (@v, @n, @s, @c)""",
                conn, tx))
            {
                record.Parameters.AddWithValue("v", file.Version);
                record.Parameters.AddWithValue("n", file.Name);
                record.Parameters.AddWithValue("s", file.FileName);
                record.Parameters.AddWithValue("c", file.Checksum);
                await record.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch (PostgresException ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw new SqlScriptException(SqlScriptRunner.Describe(file.FileName, sql, ex), ex);
        }
    }
}
