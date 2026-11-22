using Npgsql;

namespace SecureShift.ConsoleApp.Services;

/// <summary>Who/where we are connected to.</summary>
public sealed record ServerInfo(string Host, int Port, string Database, string User, string Version);

/// <summary>Which SecureShift objects exist in schema dev.</summary>
public sealed record SchemaSnapshot(
    bool SchemaExists,
    IReadOnlyList<string> PresentTables,
    IReadOnlyList<string> MissingTables,
    IReadOnlyList<string> PresentViews,
    IReadOnlyList<string> MissingViews,
    int ForeignKeyCount,
    bool HasMigrationHistory)
{
    public bool IsComplete => SchemaExists && MissingTables.Count == 0 && MissingViews.Count == 0;
    public bool IsEmpty => PresentTables.Count == 0 && PresentViews.Count == 0;
}

/// <summary>Read-only questions about the database (used by db status / setup / seed / baseline).</summary>
public sealed class DatabaseInspector(DbSettings settings)
{
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(settings.GetAdminConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<ServerInfo> GetServerInfoAsync(NpgsqlConnection conn, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand("SELECT current_database(), current_user, current_setting('server_version')", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return new ServerInfo(conn.Host ?? "?", conn.Port, r.GetString(0), r.GetString(1), r.GetString(2));
    }

    public async Task<SchemaSnapshot> GetSnapshotAsync(NpgsqlConnection conn, CancellationToken ct = default)
    {
        const string sql = """
            SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @schema);
            SELECT c.relname, c.relkind::text
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = @schema AND c.relkind IN ('r', 'p', 'v');
            SELECT COUNT(*)
            FROM pg_constraint con
            JOIN pg_class c ON c.oid = con.conrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = @schema AND con.contype = 'f' AND c.relname = ANY(@tables);
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", SchemaInfo.Schema);
        cmd.Parameters.AddWithValue("tables", SchemaInfo.Tables.ToArray());

        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        var schemaExists = r.GetBoolean(0);

        await r.NextResultAsync(ct);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        var views = new HashSet<string>(StringComparer.Ordinal);
        while (await r.ReadAsync(ct))
        {
            if (r.GetString(1) == "v") views.Add(r.GetString(0)); else tables.Add(r.GetString(0));
        }

        await r.NextResultAsync(ct);
        await r.ReadAsync(ct);
        var fkCount = (int)r.GetInt64(0);

        return new SchemaSnapshot(
            schemaExists,
            SchemaInfo.Tables.Where(tables.Contains).ToList(),
            SchemaInfo.Tables.Where(t => !tables.Contains(t)).ToList(),
            SchemaInfo.Views.Where(views.Contains).ToList(),
            SchemaInfo.Views.Where(v => !views.Contains(v)).ToList(),
            fkCount,
            tables.Contains(SchemaInfo.MigrationsTable));
    }

    /// <summary>Row count of every table that exists (in the fixed SchemaInfo order).</summary>
    public async Task<IReadOnlyList<(string Table, long Rows)>> GetRowCountsAsync(
        NpgsqlConnection conn, IEnumerable<string> tables, CancellationToken ct = default)
    {
        var result = new List<(string, long)>();
        foreach (var table in tables)
        {
            // Table names come from SchemaInfo (fixed list), never from user input.
            await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {SchemaInfo.Qualified(table)}", conn);
            var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
            result.Add((table, count));
        }
        return result;
    }
}
