using Microsoft.EntityFrameworkCore;
using SecureShift.ConsoleApp.Services;
using SecureShift.Data.Context.Generated;

namespace SecureShift.Tests;

/// <summary>
/// Shared setup for the database tests. Needs a FRESHLY SEEDED SecureShift database:
/// set the env var ConnectionStrings__SecureShiftDb (CI does this with a postgres:16 service).
/// The tests only read; they never change data.
/// </summary>
public sealed class DatabaseFixture
{
    public DbSettings Settings { get; }

    public DatabaseFixture()
    {
        Settings = new DbSettings(Environment.GetEnvironmentVariable(DbSettings.EnvironmentVariable));
        if (!Settings.IsConfigured)
        {
            throw new InvalidOperationException(
                $"The database tests need a freshly seeded SecureShift database. Set the env var {DbSettings.EnvironmentVariable} first " +
                "(see docs/backend/RUNNING.md). The unit tests do not need it.");
        }
    }

    /// <summary>A new DbContext, the same way the app creates one per action.</summary>
    public SecureShiftDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<SecureShiftDbContext>().UseNpgsql(Settings.GetConnectionString()).Options);

    public DatabaseInspector CreateInspector() => new(Settings);
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
