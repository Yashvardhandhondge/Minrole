using System.Globalization;
using SecureShift.ConsoleApp.Services;

namespace SecureShift.ConsoleApp.Menus;

/// <summary>Thrown when the user types "cancel" in a form: the form is closed and nothing is saved.</summary>
public sealed class FormCancelledException() : Exception("Cancelled.");

/// <summary>
/// Plain System.Console helpers used by every menu: titles, messages, aligned tables, numbered menus,
/// questions that are asked again until the answer is valid, YES / yes-no confirmations.
/// All input and output goes through <see cref="IConsoleIO"/>.
/// </summary>
public sealed class ConsoleUi(IConsoleIO io)
{
    public const string CancelWord = "cancel";
    public IConsoleIO IO => io;

    // ----- output -----
    public void WriteLine(string text = "") => io.WriteLine(text);
    public void Write(string text) => io.Write(text);
    public void ErrorLine(string text) => io.WriteErrorLine(text);

    public void Title(string text)
    {
        io.WriteLine();
        io.WriteColored(text, ConsoleColor.Cyan);
        io.WriteLine();
        io.WriteLine(new string('=', text.Length));
    }

    public void Section(string text)
    {
        io.WriteLine();
        io.WriteLine(text);
        io.WriteLine(new string('-', text.Length));
    }

    public void Ok(string text) => Tagged("OK", ConsoleColor.Green, text, error: false);
    public void Warn(string text) => Tagged("WARNING", ConsoleColor.Yellow, text, error: false);
    public void Info(string text) => Tagged("INFO", ConsoleColor.Gray, text, error: false);

    /// <summary>[ERROR] line. Db commands write it to stderr; menus write it to the normal output (same screen).</summary>
    public void Error(string text) => Tagged("ERROR", ConsoleColor.Red, text, error: ErrorsToStdErr);

    /// <summary>True for the db commands (scripts can tell errors apart), false inside the interactive app.</summary>
    public bool ErrorsToStdErr { get; set; } = true;

    private void Tagged(string tag, ConsoleColor color, string text, bool error)
    {
        if (error)
        {
            io.WriteErrorLine($"[{tag}] {text}");
            return;
        }
        io.WriteColored($"[{tag}] ", color);
        io.WriteLine(text);
    }

    /// <summary>Key/value lines with aligned values ("Name      : Arya").</summary>
    public void KeyValues(IEnumerable<(string Key, string Value)> pairs)
    {
        var list = pairs.ToList();
        if (list.Count == 0) return;
        var width = list.Max(p => p.Key.Length);
        foreach (var (key, value) in list) io.WriteLine($"  {key.PadRight(width)} : {value}");
    }

    /// <summary>A framed box (used for "User Created" / "Password Reset").</summary>
    public void Box(string title, IReadOnlyList<string> lines)
    {
        var width = Math.Max(title.Length, lines.Count == 0 ? 0 : lines.Max(l => l.Length)) + 2;
        io.WriteLine("+" + new string('-', width + 2) + "+");
        io.WriteLine("| " + (" " + title).PadRight(width) + " |");
        io.WriteLine("+" + new string('-', width + 2) + "+");
        foreach (var line in lines) io.WriteLine("| " + (" " + line).PadRight(width) + " |");
        io.WriteLine("+" + new string('-', width + 2) + "+");
    }

    /// <summary>Prints rows as an aligned table. Columns listed in rightAlign are right-aligned (numbers).</summary>
    public void Table(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, params int[] rightAlign)
    {
        var widths = headers.Select((h, i) => Math.Max(h.Length, rows.Count == 0 ? 0 : rows.Max(r => (r[i] ?? "").Length))).ToArray();
        string Line(IReadOnlyList<string> cells) => string.Join("  ", cells.Select((c, i) =>
            rightAlign.Contains(i) ? (c ?? "").PadLeft(widths[i]) : (c ?? "").PadRight(widths[i]))).TrimEnd();

        io.WriteLine(Line(headers));
        io.WriteLine(string.Join("  ", widths.Select(w => new string('-', w))));
        foreach (var row in rows) io.WriteLine(Line(row));
    }

    /// <summary>A simple text bar for dashboards: "#####....." for value out of max.</summary>
    public static string Bar(double value, double max, int width = 20)
    {
        if (max <= 0) return new string('.', width);
        var filled = (int)Math.Round(Math.Clamp(value / max, 0, 1) * width, MidpointRounding.AwayFromZero);
        return new string('#', filled) + new string('.', width - filled);
    }

    // ----- input -----

    /// <summary>Reads one line (trimmed). Throws <see cref="InputEndedException"/> at the end of the input.</summary>
    public string Ask(string prompt)
    {
        io.Write(prompt);
        var line = io.ReadLine() ?? throw new InputEndedException();
        return line.Trim();
    }

    /// <summary>Like Ask, but "cancel" closes the form (throws <see cref="FormCancelledException"/>).</summary>
    public string AskField(string prompt)
    {
        var answer = Ask(prompt);
        if (string.Equals(answer, CancelWord, StringComparison.OrdinalIgnoreCase)) throw new FormCancelledException();
        return answer;
    }

    /// <summary>
    /// Asks until <paramref name="validate"/> returns "" (valid). The error text is shown under the question,
    /// like the red text under a field in the prototype. Empty input keeps <paramref name="current"/> when given.
    /// </summary>
    public string AskValid(string label, Func<string, string> validate, string? current = null)
    {
        while (true)
        {
            var prompt = current is null ? $"{label}: " : $"{label} [{current}]: ";
            var answer = AskField(prompt);
            if (answer.Length == 0 && current is not null) answer = current;
            var error = validate(answer);
            if (error.Length == 0) return answer;
            Error(error);
        }
    }

    /// <summary>Asks for a whole number between min and max (SS.V.int rule).</summary>
    public int AskInt(string label, int min, int max, string message, int? current = null) =>
        int.Parse(AskValid(label, v => Validation.IsInt(v, min, max) ? "" : message, current?.ToString(CultureInfo.InvariantCulture)),
            CultureInfo.InvariantCulture);

    public long AskLong(string label, long min, long max, string message, long? current = null) =>
        long.Parse(AskValid(label, v => Validation.IsLong(v, min, max) ? "" : message, current?.ToString(CultureInfo.InvariantCulture)),
            CultureInfo.InvariantCulture);

    /// <summary>Asks the user to pick one option by number. Returns the chosen option.</summary>
    public string AskChoice(string label, IReadOnlyList<string> options, string? current = null, string? requiredMessage = null)
    {
        io.WriteLine($"{label}:");
        for (var i = 0; i < options.Count; i++) io.WriteLine($"  {i + 1}. {options[i]}");
        while (true)
        {
            var prompt = current is null ? $"Choose 1-{options.Count}: " : $"Choose 1-{options.Count} [{current}]: ";
            var answer = AskField(prompt);
            if (answer.Length == 0 && current is not null) return current;
            if (int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= options.Count)
                return options[n - 1];
            var exact = options.FirstOrDefault(o => string.Equals(o, answer, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact;
            Error(requiredMessage ?? $"Type a number from 1 to {options.Count}");
        }
    }

    /// <summary>Several options (e.g. technologies): "1,3" or "1 3". Empty input = none (unless current is given).</summary>
    public List<string> AskMulti(string label, IReadOnlyList<string> options, Func<List<string>, string> validate, IReadOnlyList<string>? current = null)
    {
        io.WriteLine($"{label}:");
        for (var i = 0; i < options.Count; i++) io.WriteLine($"  {i + 1}. {options[i]}");
        while (true)
        {
            var currentText = current is null ? null : (current.Count == 0 ? "none" : string.Join(", ", current));
            var prompt = currentText is null ? "Type the numbers, e.g. 1,3 (Enter = none): " : $"Type the numbers, e.g. 1,3 [{currentText}]: ";
            var answer = AskField(prompt);
            List<string> picked;
            if (answer.Length == 0 && current is not null) picked = current.ToList();
            else
            {
                var parts = answer.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries);
                var bad = parts.Where(p => !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1 || n > options.Count).ToList();
                if (bad.Count > 0)
                {
                    Error($"Unknown number(s): {string.Join(", ", bad)}. Use numbers from 1 to {options.Count}.");
                    continue;
                }
                picked = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).Distinct().Order().Select(n => options[n - 1]).ToList();
            }
            var error = validate(picked);
            if (error.Length == 0) return picked;
            Error(error);
        }
    }

    /// <summary>Asks for a date (MM/DD/YYYY like the prototype, or YYYY-MM-DD). Empty input = <paramref name="current"/>.</summary>
    public DateOnly? AskDate(string label, DateOnly? current, bool allowEmpty)
    {
        while (true)
        {
            var shown = current is null ? (allowEmpty ? "none" : null) : Fmt.Date(current.Value);
            var answer = AskField(shown is null ? $"{label} (MM/DD/YYYY): " : $"{label} (MM/DD/YYYY) [{shown}]: ");
            if (answer.Length == 0)
            {
                if (current is not null || allowEmpty) return current;
                Error("Enter a date like 11/22/2026");
                continue;
            }
            if (answer == "-" && allowEmpty) return null;
            if (Fmt.TryParseDate(answer, out var date)) return date;
            Error("Enter a date like 11/22/2026 (or 2026-11-22)");
        }
    }

    /// <summary>Yes / no question. Only "y" or "yes" means yes.</summary>
    public bool AskYesNo(string question)
    {
        var answer = Ask($"{question} (y/n): ");
        return answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Password question: every key shows as '*'.</summary>
    public string AskPassword(string prompt)
    {
        io.Write(prompt);
        return io.ReadPassword() ?? throw new InputEndedException();
    }

    /// <summary>Asks "Type YES to continue". Returns true only for YES (or when --yes was given).</summary>
    public bool Confirm(bool assumeYes)
    {
        if (assumeYes)
        {
            io.WriteLine("--yes was given, continuing without asking.");
            return true;
        }

        io.Write("Type YES to continue: ");
        var answer = io.ReadLine();
        if (answer?.Trim() == "YES") return true;

        io.WriteLine("Cancelled. Nothing was changed.");
        return false;
    }
}
