using System.Globalization;
using Npgsql;
using SecureShift.ConsoleApp.Services;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// The "db ..." commands. This class only prints and asks; the work is done by the Services.
/// </summary>
public sealed class DbCommands(
    DbSettings settings,
    DatabaseInspector inspector,
    SqlScriptRunner runner,
    SqlScriptFiles files,
    MigrationService migrations,
    SmokeService smoke,
    ConsoleUi ui)
{
    private static string N(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    /* ------------------------------------------------------------------ db status */

    public async Task<int> StatusAsync()
    {
        ui.Title("SecureShift - db status");
        await using var conn = await inspector.OpenAsync();
        await PrintServerAsync(conn);

        var snap = await inspector.GetSnapshotAsync(conn);
        if (!snap.SchemaExists)
        {
            ui.Error($"Schema \"{SchemaInfo.Schema}\" does not exist in this database. Ask the DB admin to create it (CREATE SCHEMA dev;).");
            return 1;
        }

        ui.Section($"SecureShift objects in schema {SchemaInfo.Schema}");
        ui.WriteLine($"Tables       : {snap.PresentTables.Count} of {SchemaInfo.Tables.Count}");
        if (snap.MissingTables.Count > 0) ui.WriteLine($"  missing    : {string.Join(", ", snap.MissingTables)}");
        ui.WriteLine($"Views        : {snap.PresentViews.Count} of {SchemaInfo.Views.Count}");
        if (snap.MissingViews.Count > 0) ui.WriteLine($"  missing    : {string.Join(", ", snap.MissingViews)}");
        ui.WriteLine($"Foreign keys : {snap.ForeignKeyCount} (expected {SchemaInfo.ExpectedForeignKeys})");

        if (snap.PresentTables.Count > 0)
        {
            ui.Section("Rows per table");
            var counts = await inspector.GetRowCountsAsync(conn, snap.PresentTables);
            var rows = counts
                .Select(c => (IReadOnlyList<string>)new[] { $"dev.\"{c.Table}\"", N(c.Rows), N(SchemaInfo.SeedRowCounts[c.Table]) })
                .ToList();
            rows.Add(["TOTAL", N(counts.Sum(c => c.Rows)), N(SchemaInfo.SeedTotalRows)]);
            ui.Table(["Table", "Rows", "Fresh seed"], rows, 1, 2);
        }

        await PrintMigrationsAsync(conn, snap);

        ui.WriteLine();
        if (snap.IsComplete && snap.ForeignKeyCount == SchemaInfo.ExpectedForeignKeys)
        {
            ui.Ok($"All {SchemaInfo.Tables.Count} tables, {SchemaInfo.Views.Count} views and {SchemaInfo.ExpectedForeignKeys} foreign keys are present.");
            return 0;
        }

        ui.Error(snap.IsEmpty
            ? "SecureShift is not set up in this schema yet. For a brand-new empty dev schema run: db setup"
            : "Some SecureShift objects are missing (see above).");
        return 1;
    }

    private async Task PrintMigrationsAsync(NpgsqlConnection conn, SchemaSnapshot snap)
    {
        ui.Section($"Migrations (dev.\"{SchemaInfo.MigrationsTable}\")");
        var plan = await migrations.PlanAsync(conn);

        if (!snap.HasMigrationHistory)
        {
            ui.WriteLine("No migration history yet.");
            ui.WriteLine("  Faculty DB (already set up by hand) -> run: db baseline");
            ui.WriteLine("  Brand-new empty dev schema          -> run: db setup");
        }
        else
        {
            ui.Table(
                ["Version", "Name", "Script", "Applied at (UTC)", "By"],
                plan.Applied.Select(a => (IReadOnlyList<string>)new[] {
                    $"V{a.Version:000}", a.Name, a.ScriptName, a.AppliedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), a.AppliedBy }).ToList());
        }

        ui.WriteLine(plan.Pending.Count == 0
            ? "Pending migration files: none"
            : $"Pending migration files: {string.Join(", ", plan.Pending.Select(p => p.FileName))}  -> run: db migrate");
        foreach (var p in plan.Problems) ui.Error(p);
        foreach (var w in plan.Warnings) ui.Warn(w);
    }

    /* ------------------------------------------------------------------ db setup */

    public async Task<int> SetupAsync(bool yes)
    {
        ui.Title("SecureShift - db setup (brand-new EMPTY dev schema only)");
        var create = files.PathOf(SqlScriptFiles.CreateScript);
        var seed = files.PathOf(SqlScriptFiles.SeedScript);
        var history = files.PathOf(SqlScriptFiles.HistoryScript);

        await using (var conn = await inspector.OpenAsync())
        {
            await PrintServerAsync(conn);
            var snap = await inspector.GetSnapshotAsync(conn);
            if (!snap.SchemaExists)
            {
                ui.Error($"Schema \"{SchemaInfo.Schema}\" does not exist. This app never creates schemas. Ask the DB admin to run: CREATE SCHEMA dev;");
                return 1;
            }
            if (!snap.IsEmpty || snap.HasMigrationHistory)
            {
                ui.Error($"Refusing: schema dev already has {snap.PresentTables.Count} SecureShift table(s) and {snap.PresentViews.Count} view(s). Nothing was changed.");
                ui.ErrorLine("'db setup' is only for a brand-new empty dev schema.");
                ui.ErrorLine("If this database was already set up by hand (the faculty DB), run 'db baseline' instead.");
                return 1;
            }
        }

        ui.WriteLine();
        ui.WriteLine("This will:");
        ui.WriteLine($"  1. run {SqlScriptFiles.CreateScript}  (create 28 tables + 4 views)");
        ui.WriteLine($"  2. run {SqlScriptFiles.SeedScript}  (load 569 sample rows)");
        ui.WriteLine($"  3. run {SqlScriptFiles.HistoryScript}  (migration history + baseline)");
        if (!ui.Confirm(yes)) return 1;

        await RunScriptAsync(create, "28 tables and 4 views created.");
        await RunScriptAsync(seed, $"{SchemaInfo.SeedTotalRows} sample rows loaded.");
        await RunScriptAsync(history, "Migration history created, baseline (version 1) recorded.");

        return await PrintTotalsAsync();
    }

    /* ------------------------------------------------------------------ db seed */

    public async Task<int> SeedAsync(bool yes)
    {
        ui.Title("SecureShift - db seed (only when all 28 tables are empty)");
        var seed = files.PathOf(SqlScriptFiles.SeedScript);

        await using (var conn = await inspector.OpenAsync())
        {
            await PrintServerAsync(conn);
            var snap = await inspector.GetSnapshotAsync(conn);
            if (snap.MissingTables.Count > 0)
            {
                ui.Error($"Refusing: {snap.MissingTables.Count} SecureShift table(s) are missing ({string.Join(", ", snap.MissingTables.Take(5))}...). Run 'db setup' on an empty dev schema instead.");
                return 1;
            }
            var withRows = (await inspector.GetRowCountsAsync(conn, SchemaInfo.Tables)).Where(c => c.Rows > 0).ToList();
            if (withRows.Count > 0)
            {
                ui.Error($"Refusing: {withRows.Count} table(s) already have rows, e.g. {string.Join(", ", withRows.Take(5).Select(c => $"{c.Table} ({c.Rows})"))}. Nothing was changed.");
                ui.ErrorLine("'db seed' only loads the sample data into EMPTY tables, so it can never create duplicates.");
                return 1;
            }
        }

        ui.WriteLine();
        ui.WriteLine($"This will run {SqlScriptFiles.SeedScript} and load {SchemaInfo.SeedTotalRows} rows into the 28 empty tables.");
        if (!ui.Confirm(yes)) return 1;

        await RunScriptAsync(seed, $"{SchemaInfo.SeedTotalRows} sample rows loaded.");
        return await PrintTotalsAsync();
    }

    /* ------------------------------------------------------------------ db migrate */

    public async Task<int> MigrateAsync(bool yes)
    {
        ui.Title("SecureShift - db migrate");
        MigrationPlan plan;
        await using (var conn = await inspector.OpenAsync())
        {
            await PrintServerAsync(conn);
            if (!await MigrationService.HistoryTableExistsAsync(conn))
            {
                ui.Error($"There is no migration history (dev.\"{SchemaInfo.MigrationsTable}\") yet. Nothing was changed.");
                ui.ErrorLine("  Faculty DB (already set up by hand) -> run 'db baseline' first.");
                ui.ErrorLine("  Brand-new empty dev schema          -> run 'db setup' first.");
                return 1;
            }
            plan = await migrations.PlanAsync(conn);
        }

        foreach (var w in plan.Warnings) ui.Warn(w);
        if (plan.Problems.Count > 0)
        {
            foreach (var p in plan.Problems) ui.Error(p);
            ui.Error("Fix the migration files above first. Nothing was changed.");
            return 1;
        }

        var latest = plan.Applied.Count == 0 ? 0 : plan.Applied.Max(a => a.Version);
        if (plan.Pending.Count == 0)
        {
            ui.Ok($"The database is up to date (latest applied version: V{latest:000}). No migration files to apply.");
            return 0;
        }

        ui.WriteLine();
        ui.WriteLine($"{plan.Pending.Count} migration file(s) will be applied, in this order, each in its own transaction:");
        foreach (var f in plan.Pending) ui.WriteLine($"  V{f.Version:000}  {f.FileName}");
        if (!ui.Confirm(yes)) return 1;

        var done = 0;
        foreach (var f in plan.Pending)
        {
            try
            {
                await migrations.ApplyAsync(f);
                done++;
                ui.Ok($"{f.FileName} applied and recorded.");
            }
            catch (SqlScriptException ex)
            {
                ui.Error(ex.Message);
                ui.ErrorLine($"Stopped. {done} migration(s) were applied before this one; {f.FileName} was rolled back; the rest were not run.");
                return 1;
            }
        }

        ui.Ok($"{done} migration(s) applied. Next: run the scaffold workflow so the EF models match (see RUNNING.md).");
        return 0;
    }

    /* ------------------------------------------------------------------ db baseline */

    public async Task<int> BaselineAsync(bool yes)
    {
        ui.Title("SecureShift - db baseline (database that was already set up by hand)");
        var history = files.PathOf(SqlScriptFiles.HistoryScript);

        await using (var conn = await inspector.OpenAsync())
        {
            await PrintServerAsync(conn);
            var snap = await inspector.GetSnapshotAsync(conn);
            if (!snap.IsComplete)
            {
                ui.Error("Refusing: this database does not have all the SecureShift tables and views, so it cannot be baselined. Nothing was changed.");
                if (snap.MissingTables.Count > 0) ui.ErrorLine($"  missing tables: {string.Join(", ", snap.MissingTables)}");
                if (snap.MissingViews.Count > 0) ui.ErrorLine($"  missing views : {string.Join(", ", snap.MissingViews)}");
                ui.ErrorLine("For a brand-new empty dev schema use 'db setup'.");
                return 1;
            }

            var applied = await MigrationService.GetAppliedAsync(conn);
            var baseline = applied.FirstOrDefault(a => a.Version == MigrationService.BaselineVersion);
            if (baseline is not null)
            {
                ui.Ok($"Already baselined on {baseline.AppliedAtUtc:yyyy-MM-dd HH:mm} UTC by {baseline.AppliedBy}. Nothing to do.");
                return 0;
            }
        }

        ui.WriteLine();
        ui.WriteLine($"This will run {SqlScriptFiles.HistoryScript}: it creates dev.\"{SchemaInfo.MigrationsTable}\" and records");
        ui.WriteLine("version 1 = baseline. It does NOT change any SecureShift table or row.");
        if (!ui.Confirm(yes)) return 1;

        await RunScriptAsync(history, "Baseline recorded. From now on use 'db migrate' for schema changes.");
        return 0;
    }

    /* ------------------------------------------------------------------ smoke */

    public async Task<int> SmokeAsync()
    {
        ui.Title("SecureShift - smoke (EF Core reads the database)");
        ui.WriteLine($"Database : {settings.Describe()}");
        ui.WriteLine();

        var counts = await smoke.CountAsync();
        ui.Table(
            ["Table", "Rows (EF Core)", "Fresh seed", "Check"],
            counts.Select(c => (IReadOnlyList<string>)new[] { c.Name, N(c.Actual), N(c.Expected), c.Matches ? "ok" : "DIFFERENT" }).ToList(),
            1, 2);

        ui.WriteLine();
        if (counts.All(c => c.Matches))
        {
            ui.Ok("EF Core can read the database and the counts match the fresh seed (25 / 5 / 10 / 3).");
            return 0;
        }

        ui.Error("EF Core can read the database, but the counts differ from the fresh seed (25 / 5 / 10 / 3).");
        ui.ErrorLine("That is normal on a database people already use; on a freshly seeded database it means something is wrong.");
        return 1;
    }

    /* ------------------------------------------------------------------ helpers */

    private async Task PrintServerAsync(NpgsqlConnection conn)
    {
        var s = await inspector.GetServerInfoAsync(conn);
        ui.WriteLine($"Server   : {s.Host}:{s.Port} (PostgreSQL {s.Version})");
        ui.WriteLine($"Database : {s.Database}");
        ui.WriteLine($"User     : {s.User}");
        ui.WriteLine($"Schema   : {SchemaInfo.Schema}");
    }

    private async Task RunScriptAsync(string path, string successMessage)
    {
        ui.Write($"Running {Path.GetFileName(path)} ... ");
        try
        {
            var notices = await runner.RunFileAsync(path);
            ui.WriteLine("done");
            foreach (var n in notices) ui.WriteLine($"  {n}");
            ui.Ok(successMessage);
        }
        catch
        {
            ui.WriteLine("FAILED");
            throw;
        }
    }

    private async Task<int> PrintTotalsAsync()
    {
        await using var conn = await inspector.OpenAsync();
        var snap = await inspector.GetSnapshotAsync(conn);
        var total = (await inspector.GetRowCountsAsync(conn, snap.PresentTables)).Sum(c => c.Rows);
        ui.WriteLine();
        ui.WriteLine($"Now: {snap.PresentTables.Count} tables, {snap.PresentViews.Count} views, {snap.ForeignKeyCount} foreign keys, {N(total)} rows.");
        if (!snap.IsComplete)
        {
            ui.Error("Some objects are missing after the scripts ran. Run 'db status' for details.");
            return 1;
        }
        ui.Ok("Done. Run 'db status' or 'smoke' to check.");
        return 0;
    }
}
