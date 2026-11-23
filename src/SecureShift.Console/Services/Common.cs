using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Context.Generated;

namespace SecureShift.ConsoleApp.Services;

/// <summary>Role codes (dev."Roles"."RoleCode") and the role names shown on screen.</summary>
public static class RoleCodes
{
    public const string CloudAdmin = "cloudadmin";
    public const string SuperAdmin = "superadmin";
    public const string Admin = "admin";
    public const string Architect = "architect";
    public const string Executive = "executive";

    public const string ProductGovernance = "Governance";
    public const string ProductModernization = "Modernization";

    /// <summary>SS.PRODUCTS names (js/data/users.js).</summary>
    public static string ProductTitle(string product) =>
        product == ProductGovernance ? "Cloud Governance" : "Application Modernization - EMDT";
}

/// <summary>One page of a list (not an entity copy: just the rows of the page + paging numbers).</summary>
public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int Pages => Math.Max(1, (Total + PageSize - 1) / PageSize);
    public int FirstNumber => Total == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastNumber => Math.Min(Total, Page * PageSize);

    public static async Task<Paged<T>> FromQueryAsync(IQueryable<T> query, int page, int pageSize)
    {
        var total = await query.CountAsync();
        var pages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pages);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new Paged<T>(items, page, pageSize, total);
    }

    public static Paged<T> FromList(IReadOnlyList<T> all, int page, int pageSize)
    {
        var pages = Math.Max(1, (all.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pages);
        return new Paged<T>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count);
    }
}

/// <summary>Writes a row to dev."AuditLogs" inside the caller's DbContext (so it is saved in the same transaction).</summary>
public static class Audit
{
    public static void Add(SecureShiftDbContext db, int userId, string action, string? target, string? detail, DateTime utcNow)
    {
        db.AuditLogs.Add(new Data.Models.Generated.AuditLog
        {
            UserId = userId,
            Action = Cut(action, 50)!,
            Target = Cut(target, 150),
            Detail = Cut(detail, 500),
            CreatedAt = utcNow,
        });
    }

    private static string? Cut(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];
}

/// <summary>Small helpers every service uses: a fresh DbContext per action and "now" in UTC.</summary>
public abstract class ServiceBase(IDbContextFactory<SecureShiftDbContext> factory, TimeProvider clock)
{
    protected IDbContextFactory<SecureShiftDbContext> Factory => factory;
    protected TimeProvider Clock => clock;

    /// <summary>A NEW DbContext for this action (never reused, so every screen reads fresh data).</summary>
    protected Task<SecureShiftDbContext> NewDbAsync() => factory.CreateDbContextAsync();

    /// <summary>Now as a UTC DateTime (Npgsql only accepts Kind=Utc for timestamptz).</summary>
    protected DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    /// <summary>Today's date in the app time zone (India), used for the DATE columns (Submitted On, decisions ...).</summary>
    protected DateOnly Today => AppTime.DateOf(UtcNow);
}
