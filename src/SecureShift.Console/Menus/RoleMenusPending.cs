using SecureShift.ConsoleApp.Services;

namespace SecureShift.ConsoleApp.Menus;

// Temporary role menus (Phase 2). Each one is replaced by the real menu in its phase.

public sealed class ArchitectMenu(ConsoleUi ui, MenuRunner runner, ProfileMenu profile)
{
    public Task RunAsync(Session session) => runner.RunAsync(session, "Architect", me =>
        (IReadOnlyList<MenuItem>)[new("Architect menus (Phase 5)", _ => { ui.Info("Comes in Phase 5."); return Task.CompletedTask; }), .. profile.CommonItems(session)], topLevel: true);
}

public sealed class ExecutiveMenu(ConsoleUi ui, MenuRunner runner, ProfileMenu profile)
{
    public Task RunAsync(Session session) => runner.RunAsync(session, "Executive", me =>
        (IReadOnlyList<MenuItem>)[new("Executive menus (Phase 5)", _ => { ui.Info("Comes in Phase 5."); return Task.CompletedTask; }), .. profile.CommonItems(session)], topLevel: true);
}
