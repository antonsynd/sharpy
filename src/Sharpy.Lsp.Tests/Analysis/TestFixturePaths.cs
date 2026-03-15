using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests.Analysis;

/// <summary>
/// Shared fixture path computation for gap discovery tests in Sharpy.Lsp.Tests.
/// Duplicates the path logic from Sharpy.Compiler.Tests FixtureDiscoveryHelper
/// since test-to-test project references are architecturally problematic.
/// </summary>
internal static class TestFixturePaths
{
    /// <summary>
    /// Absolute path to the compiler test fixtures directory.
    /// Computed relative to the test assembly output directory.
    /// </summary>
    internal static readonly string CompilerFixturesPath = IOPath.GetFullPath(
        IOPath.Combine(
            IOPath.GetDirectoryName(typeof(TestFixturePaths).Assembly.Location)!,
            "..", "..", "..", "..", "Sharpy.Compiler.Tests", "Integration", "TestFixtures"));

    /// <summary>
    /// Absolute path to the .claude/tmp directory for report output.
    /// </summary>
    internal static readonly string ReportOutputDir = IOPath.GetFullPath(
        IOPath.Combine(
            IOPath.GetDirectoryName(typeof(TestFixturePaths).Assembly.Location)!,
            "..", "..", "..", "..", "..", "..", ".claude", "tmp"));
}
