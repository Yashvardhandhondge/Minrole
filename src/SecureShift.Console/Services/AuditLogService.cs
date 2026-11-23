using Microsoft.EntityFrameworkCore;
using SecureShift.Data.Context.Generated;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Services;

/// <summary>Filters of the Audit Logs page: action, performed by, date range (dates in the app time zone, both included), search text.</summary>
public sealed record AuditFilter(string? Action = null, int? UserId = null, DateOnly? From = null, DateOnly? To = null, string? Search = null);

/// <summary>Super Admin > Audit Logs (modernization/js/audit-logs.js): newest first, 10 per page, filters.</summary>
public sealed class AuditLogService(IDbContextFactory<SecureShiftDbContext> factory, TimeProvider clock) : ServiceBase(factory, clock)
{
    public async Task<Paged<AuditLog>> QueryAsync(AuditFilter f, int page, int pageSize = 10)
    {
        await using var db = await NewDbAsync();
        var q = db.AuditLogs.AsNoTracking().Include(a => a.User).AsQueryable();
        if (!string.IsNullOrEmpty(f.Action)) q = q.Where(a => a.Action == f.Action);
        if (f.UserId is not null) q = q.Where(a => a.UserId == f.UserId);
        if (f.From is not null)
        {
            var fromUtc = LocalMidnightUtc(f.From.Value);
            q = q.Where(a => a.CreatedAt >= fromUtc);
        }
        if (f.To is not null)
        {
            var toUtc = LocalMidnightUtc(f.To.Value.AddDays(1));   // the To day is included
            q = q.Where(a => a.CreatedAt < toUtc);
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToLower();
            q = q.Where(a => (a.User.FullName + " " + a.Action + " " + (a.Target ?? "") + " " + (a.Detail ?? "")).ToLower().Contains(s));
        }
        return await Paged<AuditLog>.FromQueryAsync(q.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditLogId), page, pageSize);
    }

    /// <summary>The actions that appear in the log, sorted (the "Action" filter list).</summary>
    public async Task<List<string>> ActionsAsync()
    {
        await using var db = await NewDbAsync();
        return await db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
    }

    /// <summary>The users who appear in the log, sorted by name (the "Performed By" filter list).</summary>
    public async Task<List<User>> UsersAsync()
    {
        await using var db = await NewDbAsync();
        var ids = db.AuditLogs.Select(a => a.UserId).Distinct();
        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.UserId)).OrderBy(u => u.FullName).ToListAsync();
    }

    private static DateTime LocalMidnightUtc(DateOnly date) => AppTime.StartOfDayUtc(date);
}
