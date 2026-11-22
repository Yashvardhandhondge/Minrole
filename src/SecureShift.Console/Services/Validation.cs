using System.Globalization;
using System.Text.RegularExpressions;

namespace SecureShift.ConsoleApp.Services;

/// <summary>Thrown by services when a business rule says no. The menu shows the message as [ERROR] and nothing is saved.</summary>
public sealed class UserFacingException(string message) : Exception(message);

/// <summary>
/// The shared validation rules of the prototype (SS.V in js/common/core.js) with the exact messages
/// the prototype shows under the fields. Every method returns "" when the value is valid.
/// </summary>
public static partial class Validation
{
    // SS.V.name: letters only (single spaces between words), 3-50 characters
    public const string NameMessage = "Only letters and single spaces, 3-50 characters";
    public const string EmailMessage = "Enter a valid email (name@domain.com)";
    public const string PhoneMessage = "Exactly 10 digits, starting with 6, 7, 8 or 9";
    public const string DeptMessage = "Letters, numbers and spaces only, 3-150 characters";
    public const string PasswordMessage = "Min 8 characters with uppercase, lowercase, number and special character";

    [GeneratedRegex("^[A-Za-z]+( [A-Za-z]+)*$")] private static partial Regex NameRx();
    [GeneratedRegex("^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$")] private static partial Regex EmailRx();
    [GeneratedRegex("^[6-9][0-9]{9}$")] private static partial Regex PhoneRx();
    [GeneratedRegex("^[A-Za-z0-9 ]+$")] private static partial Regex DeptRx();
    [GeneratedRegex("^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).{8,}$")] private static partial Regex PasswordRx();
    [GeneratedRegex("^[0-9]+$")] private static partial Regex DigitsRx();
    // login.js: a looser check before looking the account up
    [GeneratedRegex(@"^\S+@\S+\.\S+$")] private static partial Regex LoginEmailRx();

    public static bool IsName(string? v) { v = (v ?? "").Trim(); return v.Length is >= 3 and <= 50 && NameRx().IsMatch(v); }
    public static bool IsEmail(string? v) => EmailRx().IsMatch((v ?? "").Trim());
    public static bool IsPhone(string? v) => PhoneRx().IsMatch((v ?? "").Trim());
    public static bool IsDept(string? v) { v = (v ?? "").Trim(); return v.Length is >= 3 and <= 150 && DeptRx().IsMatch(v); }
    public static bool IsPassword(string? v) => PasswordRx().IsMatch(v ?? "");
    public static bool IsLoginEmail(string? v) => LoginEmailRx().IsMatch(v ?? "");

    /// <summary>SS.V.int: a whole number (digits only) between min and max.</summary>
    public static bool IsInt(string? v, int min, int max) =>
        IsLong(v, min, max);

    public static bool IsLong(string? v, long min, long max)
    {
        var t = (v ?? "").Trim();
        if (t.Length == 0 || t.Length > 18 || !DigitsRx().IsMatch(t)) return false;
        var n = long.Parse(t, CultureInfo.InvariantCulture);
        return n >= min && n <= max;
    }

    public static string Name(string? v) => IsName(v) ? "" : NameMessage;
    public static string Email(string? v) => IsEmail(v) ? "" : EmailMessage;
    public static string Phone(string? v) => IsPhone(v) ? "" : PhoneMessage;
    public static string Dept(string? v) => IsDept(v) ? "" : DeptMessage;
    public static string Password(string? v) => IsPassword(v) ? "" : PasswordMessage;

    /// <summary>Collects the errors of several fields: ("Name", "Only letters ...").</summary>
    public static void ThrowIfAny(params (string Field, string Error)[] checks)
    {
        var errors = checks.Where(c => c.Error.Length > 0).Select(c => $"{c.Field}: {c.Error}").ToList();
        if (errors.Count > 0) throw new UserFacingException(string.Join(Environment.NewLine + "        ", errors));
    }
}
