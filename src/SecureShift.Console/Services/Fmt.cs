using System.Globalization;
using System.Text;

namespace SecureShift.ConsoleApp.Services;

/// <summary>Formatting used on every screen, written to look like the prototype (dates MM/DD/YYYY, Indian digit grouping, ₹ lakh).</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>11/22/2026 (SS.today in core.js).</summary>
    public static string Date(DateOnly d) => d.ToString("MM/dd/yyyy", Inv);

    public static string Date(DateOnly? d) => d is null ? "-" : Date(d.Value);

    /// <summary>11/22/2026 04:12 PM in the local time of this machine (the database stores UTC).</summary>
    public static string DateTime(System.DateTime utc) =>
        ToLocal(utc).ToString("MM/dd/yyyy hh:mm tt", Inv);

    public static string DateTime(System.DateTime? utc) => utc is null ? "-" : DateTime(utc.Value);

    public static System.DateTime ToLocal(System.DateTime utc) =>
        System.DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    /// <summary>Accepts MM/DD/YYYY (like the prototype), M/D/YYYY or YYYY-MM-DD.</summary>
    public static bool TryParseDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text.Trim(), ["MM/dd/yyyy", "M/d/yyyy", "yyyy-MM-dd"], Inv, DateTimeStyles.None, out date);

    /// <summary>Indian digit grouping, same as SS.fmt (toLocaleString("en-IN")): 4500000 -> 45,00,000.</summary>
    public static string Indian(long value)
    {
        var negative = value < 0;
        var digits = Math.Abs(value).ToString(Inv);
        if (digits.Length <= 3) return (negative ? "-" : "") + digits;
        var last3 = digits[^3..];
        var rest = digits[..^3];
        var sb = new StringBuilder();
        for (var i = 0; i < rest.Length; i++)
        {
            if (i > 0 && (rest.Length - i) % 2 == 0) sb.Append(',');
            sb.Append(rest[i]);
        }
        return (negative ? "-" : "") + sb + "," + last3;
    }

    /// <summary>"₹22.15 lakh" (SS.EMC.lakh).</summary>
    public static string Lakh(decimal amount) => "₹" + amount.ToString("0.00", Inv) + " lakh";

    public static string Lakh(double amount) => Lakh((decimal)amount);

    /// <summary>Indian grouping for money in rupees: ₹50,000.</summary>
    public static string Rupees(long amount) => "₹" + Indian(amount);

    /// <summary>77.87 (always 2 decimals, like score.toFixed(2)).</summary>
    public static string Dec2(decimal value) => value.ToString("0.00", Inv);

    public static string Dec2(double value) => ((decimal)value).ToString("0.00", Inv);

    /// <summary>Numbers as JS prints them: 16.44, 22, 0.6 (no trailing zeros).</summary>
    public static string Js(double value) => ((decimal)value).ToString("0.##", Inv);

    /// <summary>Cuts long text for table cells.</summary>
    public static string Cut(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}
