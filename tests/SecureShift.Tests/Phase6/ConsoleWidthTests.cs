using SecureShift.ConsoleApp.Menus;

namespace SecureShift.Tests.Phase6;

/// <summary>Found while recording the full demo video (terminal about 126 columns): long lines and wide tables must fit.</summary>
public class ConsoleWidthTests
{
    private static (ConsoleUi Ui, ScriptedConsoleIO Io) Narrow(int width)
    {
        var io = new ScriptedConsoleIO { Width = width };
        return (new ConsoleUi(io), io);
    }

    private static string[] Lines(ScriptedConsoleIO io) => io.Output.TrimEnd('\n').Split('\n');

    [Fact]
    public void Long_lines_are_wrapped_at_spaces_and_keep_their_indentation()
    {
        var (ui, io) = Narrow(41);   // 40 usable characters
        ui.WriteLine("  Findings: one two three four five six seven eight nine ten eleven twelve");
        var lines = Lines(io);
        Assert.True(lines.Length > 1);
        Assert.All(lines, l => Assert.True(l.Length <= 40, l));
        Assert.All(lines.Skip(1), l => Assert.StartsWith("  ", l));
        Assert.Equal("Findings: one two three four five six seven eight nine ten eleven twelve",
            string.Join(" ", lines.Select(l => l.Trim())));
    }

    [Fact]
    public void Tagged_lines_wrap_too()
    {
        var (ui, io) = Narrow(41);
        ui.Ok("PLAN-007 submitted for approval and locked: it can no longer be edited by the architect");
        var lines = Lines(io);
        Assert.StartsWith("[OK] PLAN-007", lines[0]);
        Assert.All(lines, l => Assert.True(l.Length <= 40, l));
    }

    [Fact]
    public void A_message_with_one_error_per_line_is_wrapped_line_by_line()
    {
        // found in the recording: the field errors of a password change were wrapped as one long line
        var (ui, io) = Narrow(131);
        ui.ErrorsToStdErr = false;   // like inside the app
        ui.Error("Current password: Current password is wrong\n        New password: Min 8 characters with uppercase, lowercase, number and special character\n        Confirm: Passwords do not match");
        Assert.Equal(
            ["[ERROR] Current password: Current password is wrong",
             "        New password: Min 8 characters with uppercase, lowercase, number and special character",
             "        Confirm: Passwords do not match"],
            Lines(io));
    }

    [Fact]
    public void A_wide_table_is_narrowed_with_an_ellipsis_and_never_wraps()
    {
        var (ui, io) = Narrow(61);   // 60 usable characters
        ui.Table(["Code", "Name", "Description", "Score"],
            [["APP-001", "Customer360", new string('x', 80), "47"], ["APP-002", "Payments", "short", "100"]], 3);
        var lines = Lines(io);
        Assert.All(lines, l => Assert.True(l.Length <= 60, l));
        Assert.Contains("…", lines[2]);
        Assert.EndsWith("47", lines[2]);    // the right-aligned number column is never cut
        Assert.EndsWith("100", lines[3]);
        Assert.StartsWith("APP-001  Customer360", lines[2]);
    }

    [Fact]
    public void Piped_output_and_tests_are_never_wrapped()
    {
        var io = new ScriptedConsoleIO();
        var ui = new ConsoleUi(io);
        var text = new string('a', 500) + " " + new string('b', 500);
        ui.WriteLine(text);
        Assert.Equal(text + "\n", io.Output);
    }
}
