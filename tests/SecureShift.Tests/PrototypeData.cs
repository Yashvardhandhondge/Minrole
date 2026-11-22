using System.Text.Json;

namespace SecureShift.Tests;

/// <summary>
/// The numbers the REAL prototype JavaScript calculates (Data/prototype-expected.json, written by
/// tests/prototype/prototype-expected.js, which runs js/data/*.js in Node; CI checks the file is up to date).
/// </summary>
public static class PrototypeData
{
    private static readonly Lazy<JsonDocument> Doc = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "prototype-expected.json"))));

    public static JsonElement Gov => Doc.Value.RootElement.GetProperty("gov");
    public static JsonElement Emdt => Doc.Value.RootElement.GetProperty("emdt");

    public static IEnumerable<JsonElement> Items(this JsonElement e, string name) => e.GetProperty(name).EnumerateArray();
    public static string Str(this JsonElement e, string name) => e.GetProperty(name).GetString()!;
    public static int Int(this JsonElement e, string name) => e.GetProperty(name).GetInt32();
    public static double Num(this JsonElement e, string name) => e.GetProperty(name).GetDouble();
}

/// <summary>The sample input files of the Analysis Wizard, as copied next to the test assembly.</summary>
public static class Samples
{
    public static string Path(string file) => System.IO.Path.Combine(AppContext.BaseDirectory, "samples", "governance", file);
    public static string Assignments => Path("role-assignments.json");
    public static string Activity => Path("activity-log-90d.json");
    public static string Catalog => Path("roles-catalog.json");
    public static readonly DateOnly From = new(2026, 8, 12);
    public static readonly DateOnly To = new(2026, 11, 9);
}
