namespace Sharpy.Compiler.Tests.Properties;

/// <summary>
/// Iteration counts the property lanes share (#2324, P63b Decision 9, ruled (a) by the owner 2026-10-24):
/// every injection lane samples <see cref="Injection"/> programs in the suite (100), and
/// <see cref="Variable"/> overrides it — the census runs only when the variable is set and samples that
/// many (600 for the record). Switching the in-suite count is this one constant.
/// </summary>
internal static class PropertyIterations
{
    public const string Variable = "SHARPY_PROPERTY_ITER";

    private const int InSuiteInjection = 100;

    /// <summary>The override, or null when <see cref="Variable"/> is unset or not a positive integer.</summary>
    public static int? Override =>
        int.TryParse(Environment.GetEnvironmentVariable(Variable), out var value) && value > 0 ? value : null;

    /// <summary>Samples per injection lane: <see cref="Override"/>, else 100.</summary>
    public static int Injection => Override ?? InSuiteInjection;
}
