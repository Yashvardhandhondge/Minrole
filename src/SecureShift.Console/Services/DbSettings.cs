using Npgsql;

namespace SecureShift.ConsoleApp.Services;

/// <summary>Thrown when the connection string is missing or still has the placeholders.</summary>
public sealed class DbConfigurationException(string message) : Exception(message);

/// <summary>
/// The connection string "SecureShiftDb". appsettings.json only has placeholders (YOUR_HOST ...);
/// the real value comes from the env var ConnectionStrings__SecureShiftDb or appsettings.Local.json.
/// </summary>
public sealed class DbSettings(string? connectionString)
{
    public const string ConnectionStringName = "SecureShiftDb";
    public const string EnvironmentVariable = "ConnectionStrings__SecureShiftDb";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(connectionString) &&
        !connectionString.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

    /// <summary>The connection string, or a clear error telling the user how to set it.</summary>
    public string GetConnectionString()
    {
        if (!IsConfigured)
        {
            throw new DbConfigurationException(
                "The database connection string is not set (appsettings.json only has placeholders)." + Environment.NewLine +
                "Set it in PowerShell before you run the app, for example:" + Environment.NewLine +
                $"  $env:{EnvironmentVariable} = \"Host=YOUR_HOST;Port=5432;Database=YOUR_DATABASE;Username=YOUR_USERNAME;Password=YOUR_PASSWORD\"" + Environment.NewLine +
                "or create src/SecureShift.Console/appsettings.Local.json (see docs/backend/RUNNING.md).");
        }

        try
        {
            _ = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException ex)
        {
            throw new DbConfigurationException($"The connection string is not valid: {ex.Message}");
        }

        return connectionString!;
    }

    /// <summary>Connection string for the db commands: no pooling, longer command timeout for scripts.</summary>
    public string GetAdminConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(GetConnectionString())
        {
            Pooling = false,
            CommandTimeout = 300,
        };
        return builder.ConnectionString;
    }

    /// <summary>"host:port / database / user" without the password, safe to print.</summary>
    public string Describe()
    {
        if (!IsConfigured) return "(not configured)";
        var b = new NpgsqlConnectionStringBuilder(GetConnectionString());
        return $"{b.Host}:{b.Port} / {b.Database} / {b.Username}";
    }
}
