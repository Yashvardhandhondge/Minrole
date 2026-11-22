using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecureShift.ConsoleApp.Menus;
using SecureShift.ConsoleApp.Services;
using SecureShift.Data.Context.Generated;

namespace SecureShift.ConsoleApp.Hosting;

/// <summary>
/// Builds the .NET Generic Host: configuration + dependency injection.
/// Settings are read in this order (later ones win):
///   1. appsettings.json         (placeholders only, committed)
///   2. appsettings.Local.json   (your real values, gitignored, optional)
///   3. environment variables    (e.g. ConnectionStrings__SecureShiftDb)
/// </summary>
public static class AppHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            // We add the configuration sources ourselves (below) and keep the console free of log noise.
            DisableDefaults = true,
        });

        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        AddSecureShift(builder.Services, builder.Configuration);
        return builder.Build();
    }

    public static IServiceCollection AddSecureShift(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new DbSettings(configuration.GetConnectionString(DbSettings.ConnectionStringName)));
        services.AddSingleton(SqlScriptFiles.Default());
        services.AddSingleton<SqlScriptRunner>();
        services.AddSingleton<DatabaseInspector>();
        services.AddSingleton<MigrationService>();

        // EF Core: every screen/action asks the factory for a NEW DbContext (fresh data, no stale cache).
        // The options (and so the connection string check) are only built when a DB command needs them.
        services.AddDbContextFactory<SecureShiftDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<DbSettings>().GetConnectionString()));
        services.AddSingleton<SmokeService>();

        services.AddSingleton<DbCommands>();
        services.AddSingleton<CommandRouter>();
        return services;
    }
}
