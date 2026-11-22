namespace SecureShift.ConsoleApp.Menus;

/// <summary>Small helpers for plain System.Console output: titles, messages, aligned tables, YES prompt.</summary>
public static class ConsoleUi
{
    public static void Title(string text)
    {
        Console.WriteLine();
        Console.WriteLine(text);
        Console.WriteLine(new string('=', text.Length));
    }

    public static void Section(string text)
    {
        Console.WriteLine();
        Console.WriteLine(text);
        Console.WriteLine(new string('-', text.Length));
    }

    public static void Ok(string text) => Tagged("OK", ConsoleColor.Green, text, Console.Out);
    public static void Warn(string text) => Tagged("WARNING", ConsoleColor.Yellow, text, Console.Out);
    public static void Error(string text) => Tagged("ERROR", ConsoleColor.Red, text, Console.Error);

    private static void Tagged(string tag, ConsoleColor color, string text, TextWriter writer)
    {
        var old = Console.ForegroundColor;
        Console.ForegroundColor = color;
        writer.Write($"[{tag}] ");
        Console.ForegroundColor = old;
        writer.WriteLine(text);
    }

    /// <summary>Prints rows as an aligned table. Columns listed in rightAlign are right-aligned (numbers).</summary>
    public static void Table(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, params int[] rightAlign)
    {
        var widths = headers.Select((h, i) => Math.Max(h.Length, rows.Count == 0 ? 0 : rows.Max(r => r[i].Length))).ToArray();
        string Line(IReadOnlyList<string> cells) => string.Join("  ", cells.Select((c, i) =>
            rightAlign.Contains(i) ? c.PadLeft(widths[i]) : c.PadRight(widths[i]))).TrimEnd();

        Console.WriteLine(Line(headers));
        Console.WriteLine(string.Join("  ", widths.Select(w => new string('-', w))));
        foreach (var row in rows) Console.WriteLine(Line(row));
    }

    /// <summary>Asks "Type YES to continue". Returns true only for YES (or when --yes was given).</summary>
    public static bool Confirm(bool assumeYes)
    {
        if (assumeYes)
        {
            Console.WriteLine("--yes was given, continuing without asking.");
            return true;
        }

        Console.Write("Type YES to continue: ");
        var answer = Console.ReadLine();
        if (answer?.Trim() == "YES") return true;

        Console.WriteLine("Cancelled. Nothing was changed.");
        return false;
    }
}
