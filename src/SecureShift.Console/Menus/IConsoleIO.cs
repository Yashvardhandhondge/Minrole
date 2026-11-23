namespace SecureShift.ConsoleApp.Menus;

/// <summary>
/// Every byte the app reads from or writes to the terminal goes through this interface,
/// so menus can be tested with scripted input (tests use a fake that reads from a list of lines).
/// </summary>
public interface IConsoleIO
{
    void Write(string text);
    void WriteLine(string text = "");

    /// <summary>Writes text in a colour (the test fake ignores the colour).</summary>
    void WriteColored(string text, ConsoleColor color);

    /// <summary>Writes to the error stream (stderr). Used for [ERROR] lines of the db commands.</summary>
    void WriteErrorLine(string text);

    /// <summary>One line of input, or null when the input has ended (EOF / Ctrl+Z / closed pipe).</summary>
    string? ReadLine();

    /// <summary>A password: every key is shown as '*'. Null when the input has ended.</summary>
    string? ReadPassword();

    /// <summary>Clears the screen when it is a real terminal (does nothing when the output is redirected).</summary>
    void Clear();
}

/// <summary>Thrown when the input ends (EOF, closed pipe). The app then says goodbye and exits with code 0.</summary>
public sealed class InputEndedException() : Exception("The input has ended.");

/// <summary>The real terminal.</summary>
public sealed class SystemConsoleIO : IConsoleIO
{
    public void Write(string text) => Console.Write(text);
    public void WriteLine(string text = "") => Console.WriteLine(text);

    public void WriteColored(string text, ConsoleColor color)
    {
        if (Console.IsOutputRedirected)
        {
            Console.Write(text);
            return;
        }
        var old = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = old;
    }

    public void WriteErrorLine(string text) => Console.Error.WriteLine(text);

    private bool firstLine = true;

    public string? ReadLine() => Clean(Console.ReadLine());

    /// <summary>
    /// Windows PowerShell 5.1 pipes text to programs as UTF-8 WITH a byte-order mark, so the first line of
    /// piped input would start with an invisible U+FEFF (or "ï»¿" when read with an old code page).
    /// </summary>
    public static string? StripBom(string? line)
    {
        if (line is null) return null;
        line = line.TrimStart('﻿');
        return line.StartsWith("ï»¿", StringComparison.Ordinal) ? line[3..] : line;
    }

    private string? Clean(string? line)
    {
        if (!firstLine || line is null) return line;
        firstLine = false;
        return StripBom(line);
    }

    public string? ReadPassword()
    {
        // Piped input (CI end-to-end test): there are no keys to read, just read the line.
        if (Console.IsInputRedirected) return Clean(Console.ReadLine());
        firstLine = false;

        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string(chars.ToArray());
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                    Console.Write("\b \b");
                }
                continue;
            }
            // Ctrl+Z / Ctrl+D on an empty password = end of input
            if ((key.Key == ConsoleKey.Z || key.Key == ConsoleKey.D) && key.Modifiers.HasFlag(ConsoleModifiers.Control) && chars.Count == 0)
            {
                Console.WriteLine();
                return null;
            }
            if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
                Console.Write('*');
            }
        }
    }

    public void Clear()
    {
        if (Console.IsOutputRedirected) return;
        try { Console.Clear(); } catch (IOException) { /* not a real console */ }
    }
}
