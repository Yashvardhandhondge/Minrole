using SecureShift.ConsoleApp.Services;

namespace SecureShift.Tests;

/// <summary>The database has exactly what 01 + 02 create: 28 tables, 4 views, 46 FKs, 569 rows.</summary>
[Collection(DatabaseCollection.Name)]
public class SchemaTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Schema_dev_has_28_tables_4_views_and_46_foreign_keys()
    {
        var inspector = fixture.CreateInspector();
        await using var conn = await inspector.OpenAsync();
        var snap = await inspector.GetSnapshotAsync(conn);

        Assert.True(snap.SchemaExists);
        Assert.Empty(snap.MissingTables);
        Assert.Empty(snap.MissingViews);
        Assert.Equal(28, snap.PresentTables.Count);
        Assert.Equal(4, snap.PresentViews.Count);
        Assert.Equal(46, snap.ForeignKeyCount);
    }

    [Fact]
    public async Task Seed_has_569_rows_with_the_expected_count_per_table()
    {
        var inspector = fixture.CreateInspector();
        await using var conn = await inspector.OpenAsync();
        var counts = await inspector.GetRowCountsAsync(conn, SchemaInfo.Tables);

        Assert.Equal(28, counts.Count);
        foreach (var (table, rows) in counts)
            Assert.True(SchemaInfo.SeedRowCounts[table] == rows, $"dev.\"{table}\": expected {SchemaInfo.SeedRowCounts[table]} rows, found {rows}");
        Assert.Equal(569, counts.Sum(c => c.Rows));
    }
}
