namespace Sharpy.Compiler.Tests.Properties;

/// <summary>
/// Iteration counts the property lanes share (#2324, P63b Decision 9, ruled (a) by the owner 2026-10-24):
/// every injection lane samples <see cref="Injection"/> programs in the suite (100), and
/// <see cref="Variable"/> overrides it. The census runs only when <see cref="CensusVariable"/> is <c>1</c> and
/// samples <see cref="Injection"/> programs per row (600 for the record). Two variables, because a job that
/// raises the lanes' count (the nightly soak) must not also switch on a report that outruns its hang
/// timeout (#2392). Switching the in-suite count is this one constant.
/// </summary>
internal static class PropertyIterations
{
    public const string Variable = "SHARPY_PROPERTY_ITER";

    public const string CensusVariable = "SHARPY_CENSUS";

    private const int InSuiteInjection = 100;

    /// <summary>The override, or null when <see cref="Variable"/> is unset or not a positive integer.</summary>
    public static int? Override =>
        int.TryParse(Environment.GetEnvironmentVariable(Variable), out var value) && value > 0 ? value : null;

    /// <summary>Samples per injection lane: <see cref="Override"/>, else 100.</summary>
    public static int Injection => Override ?? InSuiteInjection;

    /// <summary>Whether the owner asked for the mutation census: <see cref="CensusVariable"/> is <c>1</c>.</summary>
    public static bool CensusRequested => Environment.GetEnvironmentVariable(CensusVariable) == "1";
}
