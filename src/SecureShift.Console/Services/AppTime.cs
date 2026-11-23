namespace SecureShift.ConsoleApp.Services;

/// <summary>
/// The time zone of the app ("App:TimeZone" in appsettings.json, default Asia/Kolkata = India).
/// The database stores timestamps in UTC; every time on screen and every date-only column
/// (Submitted On, decision dates, report dates, "today") uses this zone, so 01:00 in India is already the new day.
/// </summary>
public static class AppTime
{
    public const string DefaultZoneId = "Asia/Kolkata";
    public const string WindowsFallbackId = "India Standard Time";

    public static TimeZoneInfo Zone { get; private set; } = Resolve(DefaultZoneId);

    /// <summary>Called once at start-up with the configured id (empty = the default).</summary>
    public static void Configure(string? zoneId) => Zone = Resolve(string.IsNullOrWhiteSpace(zoneId) ? DefaultZoneId : zoneId.Trim());

    /// <summary>
    /// The zone for an id: the id itself (IANA like "Asia/Kolkata" or Windows like "India Standard Time"), then the
    /// same zone under its other name (IANA &lt;-&gt; Windows), then "India Standard Time" (older Windows without IANA
    /// ids). For the India ids a fixed UTC+05:30 zone is the last resort (India has no daylight saving time), so the
    /// India date is right on every machine; any other unknown id falls back to UTC.
    /// </summary>
    public static TimeZoneInfo Resolve(string zoneId, Func<string, TimeZoneInfo?>? find = null)
    {
        find ??= TryFind;
        var zone = find(zoneId);
        if (zone is null && TimeZoneInfo.TryConvertIanaIdToWindowsId(zoneId, out var windowsId)) zone = find(windowsId);
        if (zone is null && TimeZoneInfo.TryConvertWindowsIdToIanaId(zoneId, out var ianaId)) zone = find(ianaId);
        zone ??= find(WindowsFallbackId);
        if (zone is null && (zoneId == DefaultZoneId || zoneId == WindowsFallbackId))
            zone = TimeZoneInfo.CreateCustomTimeZone(DefaultZoneId, TimeSpan.FromHours(5.5), WindowsFallbackId, WindowsFallbackId);
        return zone ?? TimeZoneInfo.Utc;
    }

    private static TimeZoneInfo? TryFind(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }

    /// <summary>A UTC time (timestamptz from the DB) as the time in the app zone.</summary>
    public static DateTime ToZone(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>The calendar date (in the app zone) of a UTC time.</summary>
    public static DateOnly DateOf(DateTime utc) => DateOnly.FromDateTime(ToZone(utc));

    public static DateOnly DateOf(DateTimeOffset time) => DateOf(time.UtcDateTime);

    /// <summary>Midnight at the start of a date in the app zone, as UTC (for date filters).</summary>
    public static DateTime StartOfDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), Zone);
}
