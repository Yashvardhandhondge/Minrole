using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SecureShift.ConsoleApp.Services;
using SecureShift.Data.Context.Generated;
using SecureShift.Data.Models.Generated;

namespace SecureShift.Tests;

/// <summary>The scaffolded SecureShiftDbContext can read every table and view of the real database.</summary>
[Collection(DatabaseCollection.Name)]
public class DbContextTests(DatabaseFixture fixture)
{
    private static readonly Dictionary<string, int> ViewRowCounts = new()
    {
        ["vw_UserLogin"] = 25,        // one row per user
        ["vw_LatestAssessment"] = 5,  // 5 assessed applications, one assessment each
        ["vw_PlanSummary"] = 6,       // one row per plan
        ["vw_IdentityRisk"] = 128,    // one row per identity score
    };

    private static List<PropertyInfo> DbSetProperties() =>
        typeof(SecureShiftDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToList();

    [Fact]
    public void Context_has_32_DbSets_28_tables_and_4_keyless_views()
    {
        using var db = fixture.CreateContext();
        var sets = DbSetProperties();
        var entityTypes = sets.Select(p => db.Model.FindEntityType(p.PropertyType.GetGenericArguments()[0])!).ToList();

        Assert.Equal(32, sets.Count);
        Assert.Equal(28, entityTypes.Count(e => e.GetTableName() is not null));
        Assert.Equal(4, entityTypes.Count(e => e.GetViewName() is not null && e.FindPrimaryKey() is null));
        Assert.All(entityTypes, e => Assert.Equal("dev", e.GetSchema() ?? e.GetViewSchema()));
    }

    [Fact]
    public async Task Every_DbSet_can_be_queried_including_the_4_views()
    {
        await using var db = fixture.CreateContext();
        var loadAll = typeof(DbContextTests).GetMethod(nameof(LoadAllAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

        foreach (var set in DbSetProperties())
        {
            var clrType = set.PropertyType.GetGenericArguments()[0];
            var entityType = db.Model.FindEntityType(clrType)!;
            var dbName = entityType.GetTableName() ?? entityType.GetViewName()!;
            var expected = SchemaInfo.SeedRowCounts.TryGetValue(dbName, out var n) ? n : ViewRowCounts[dbName];

            // Loads every row, so every column of every row must map to its .NET type.
            var rows = await (Task<int>)loadAll.MakeGenericMethod(clrType).Invoke(null, [db])!;
            Assert.True(rows == expected, $"db.{set.Name} (dev.\"{dbName}\"): expected {expected} rows, got {rows}");
        }
    }

    private static async Task<int> LoadAllAsync<T>(SecureShiftDbContext db) where T : class =>
        (await db.Set<T>().AsNoTracking().ToListAsync()).Count;

    [Fact]
    public void Bool_columns_with_a_TRUE_default_are_always_sent_on_insert()
    {
        // Model check only (no query): see Partials/SecureShiftDbContext.Partial.cs.
        using var db = fixture.CreateContext();
        Assert.Equal(ValueGenerated.Never, db.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.MustChangePassword))!.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, db.Model.FindEntityType(typeof(GeneratedRole))!.FindProperty(nameof(GeneratedRole.IncludeBaselineRead))!.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, db.Model.FindEntityType(typeof(RuleSetting))!.FindProperty(nameof(RuleSetting.IsActive))!.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, db.Model.FindEntityType(typeof(HighRiskPattern))!.FindProperty(nameof(HighRiskPattern.IsEnabled))!.ValueGenerated);
    }
}
