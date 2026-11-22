namespace SecureShift.ConsoleApp.Services;

/// <summary>
/// JavaScript number helpers, so the C# formulas give exactly the numbers the prototype gives.
/// All prototype maths uses IEEE doubles, so we do too (and convert to decimal only to store).
/// </summary>
public static class JsMath
{
    /// <summary>JavaScript Math.round: rounds .5 up (towards +infinity), unlike C# Math.Round (banker's rounding).</summary>
    public static double Round(double x) => Math.Floor(x + 0.5);

    /// <summary>round2 in emdt-calc.js: Math.round(n * 100) / 100.</summary>
    public static double Round2(double n) => Math.Floor(n * 100 + 0.5) / 100;

    public static int RoundInt(double x) => (int)Round(x);
}
