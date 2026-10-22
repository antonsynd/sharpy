using Xunit;

namespace Sharpy.TestInfrastructure;

/// <summary>
/// The one gate for a test whose subject cannot run here (#2342, R-IH; verification contract §2): a
/// missing PRECONDITION — an asset this build or RID does not ship, an optional tool — reports
/// SKIPPED with its reason; it never writes a note and returns, which xunit counts Passed. A failed
/// STEP of the test itself is never a precondition: it fails. The caller is a
/// <c>[SkippableFact]</c>/<c>[SkippableTheory]</c> (under a plain <c>[Fact]</c> the
/// <see cref="SkipException"/> is a failure); <c>PythonOracleSkipCensusTests</c> enforces both.
/// Sibling of <see cref="Oracles.PythonInterpreter.RequireOrSkip(int)"/>, the python oracle's gate.
/// </summary>
public static class TestPreconditions
{
    /// <summary>Skips the calling test with <paramref name="reason"/> unless <paramref name="condition"/> holds.</summary>
    public static void RequireOrSkip(bool condition, string reason)
    {
        if (!condition)
            throw new SkipException(reason);
    }
}
