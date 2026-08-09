using Sharpy.TestInfrastructure.Integration;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests.Analysis;

/// <summary>
/// Shared fixture path computation for gap discovery tests in Sharpy.Lsp.Tests.
/// </summary>
internal static class TestFixturePaths
{
    /// <summary>
    /// Absolute path to the compiler test fixtures directory. This used to be a hand-rolled copy
    /// of the anchoring logic — one of four — which is how the two corpora stayed invisible to
    /// each other; it delegates to the named root now (#1338).
    /// </summary>
    internal static readonly string CompilerFixturesPath = FixtureRoots.CompilerTests.Path;

    /// <summary>
    /// Absolute path to the .claude/tmp directory for report output.
    /// </summary>
    internal static readonly string ReportOutputDir = IOPath.GetFullPath(
        IOPath.Combine(
            IOPath.GetDirectoryName(typeof(TestFixturePaths).Assembly.Location)!,
            "..", "..", "..", "..", "..", "..", ".claude", "tmp"));
}
