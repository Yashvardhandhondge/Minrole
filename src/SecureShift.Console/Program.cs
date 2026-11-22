// SecureShift console - entry point.
//   dotnet run --project src/SecureShift.Console               -> the app (login menu comes in Phase 2)
//   dotnet run --project src/SecureShift.Console -- db status  -> database commands (see "help")
using Microsoft.Extensions.DependencyInjection;
using SecureShift.ConsoleApp.Hosting;
using SecureShift.ConsoleApp.Menus;

Console.OutputEncoding = System.Text.Encoding.UTF8;

using var host = AppHost.Build(args);
var router = host.Services.GetRequiredService<CommandRouter>();
return await router.RunAsync(args);
