// SecureShift console - entry point.
//   dotnet run --project src/SecureShift.Console               -> the app (login, then the menu of your role)
//   dotnet run --project src/SecureShift.Console -- db status  -> database commands (see "help")
using Microsoft.Extensions.DependencyInjection;
using SecureShift.ConsoleApp.Hosting;
using SecureShift.ConsoleApp.Menus;

Console.OutputEncoding = System.Text.Encoding.UTF8;
if (!Console.IsInputRedirected) Console.InputEncoding = System.Text.Encoding.UTF8;

// Ctrl+C: stop cleanly (no stack trace). Every write runs in a transaction, so a half-done action is rolled back.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("Stopped (Ctrl+C). Goodbye.");
    Environment.Exit(0);
};

using var host = AppHost.Build(args);
var router = host.Services.GetRequiredService<CommandRouter>();
return await router.RunAsync(args);
