using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using SecureShift.Data.Models.Generated;

namespace SecureShift.ConsoleApp.Services;

public enum PasswordCheck
{
    Failed,
    /// <summary>Correct, the stored hash is already an Identity V3 hash.</summary>
    Success,
    /// <summary>Correct, but the stored hash is old (legacy SHA-256 or an older Identity format): save a new V3 hash.</summary>
    SuccessUpgrade,
}

/// <summary>
/// Password hashing. New hashes are ASP.NET Core Identity V3 (PBKDF2-HMACSHA512, 100,000 iterations),
/// the same format as the seed data. Old SQL Server style hashes (uppercase SHA-256 hex) are still accepted
/// and are upgraded to V3 by <see cref="AuthService"/> on the next successful login.
/// </summary>
public sealed partial class PasswordService
{
    private readonly PasswordHasher<User> hasher = new();

    [GeneratedRegex("^[0-9A-Fa-f]{64}$")] private static partial Regex LegacyHexRx();

    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public static bool IsLegacySha256(string? hash) => hash is not null && LegacyHexRx().IsMatch(hash);

    /// <summary>Uppercase hex SHA-256 of the UTF-8 password (the format of database/02_seed_data.sql).</summary>
    public static string LegacySha256Hex(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    public PasswordCheck Verify(User user, string password)
    {
        var stored = user.PasswordHash ?? "";
        if (IsLegacySha256(stored))
        {
            var expected = Encoding.ASCII.GetBytes(LegacySha256Hex(password));
            var actual = Encoding.ASCII.GetBytes(stored.ToUpperInvariant());
            return CryptographicOperations.FixedTimeEquals(expected, actual) ? PasswordCheck.SuccessUpgrade : PasswordCheck.Failed;
        }

        try
        {
            return hasher.VerifyHashedPassword(user, stored, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessUpgrade,
                _ => PasswordCheck.Failed,
            };
        }
        catch (FormatException)
        {
            return PasswordCheck.Failed;   // not base64: not a valid hash at all
        }
    }

    /// <summary>Temp@NNNNx, NNNN = 1000-9999 (tempPassword() in modernization/js/users.js).</summary>
    public static string NewTemporaryPassword() =>
        "Temp@" + RandomNumberGenerator.GetInt32(1000, 10000) + "x";
}
