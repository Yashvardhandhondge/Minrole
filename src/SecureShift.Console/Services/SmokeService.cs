using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Context.Generated;

namespace SecureShift.ConsoleApp.Services;

/// <summary>One line of the smoke test: what EF Core counted vs. the fresh seed.</summary>
public sealed record SmokeCount(string Name, int Actual, int Expected)
{
    public bool Matches => Actual == Expected;
}

/// <summary>
/// "smoke": proves the scaffolded EF Core model can read the real database.
/// A new DbContext per call (IDbContextFactory) + AsNoTracking, like every screen will do.
/// </summary>
public sealed class SmokeService(IDbContextFactory<SecureShiftDbContext> contextFactory)
{
    public async Task<IReadOnlyList<SmokeCount>> CountAsync(CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return
        [
            new("Users", await db.Users.AsNoTracking().CountAsync(ct), SchemaInfo.SeedRowCounts["Users"]),
            new("Roles", await db.Roles.AsNoTracking().CountAsync(ct), SchemaInfo.SeedRowCounts["Roles"]),
            new("Companies", await db.Companies.AsNoTracking().CountAsync(ct), SchemaInfo.SeedRowCounts["Companies"]),
            new("Enterprises", await db.Enterprises.AsNoTracking().CountAsync(ct), SchemaInfo.SeedRowCounts["Enterprises"]),
        ];
    }
}
