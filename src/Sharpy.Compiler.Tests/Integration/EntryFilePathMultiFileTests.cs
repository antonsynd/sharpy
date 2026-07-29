using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Drives every multi-file fixture through the <c>sharpyc run &lt;main.spy&gt;</c> entry-file seam
/// (<see cref="CompilerApi.CompileFile"/>) instead of the project seam, and asserts the same
/// <c>main.expected</c>/<c>main.error</c> sidecars the project arm asserts (#1171).
/// </summary>
/// <remarks>
/// <para>
/// The two seams differ in one thing the fixture suite never used to cover: the project seam is
/// handed a <c>ProjectConfig</c> (with a root namespace and an explicit source-file list), while the
/// entry-file seam has to discover the local-import closure and decide the namespace itself. A
/// program that runs under a <c>.spyproj</c> but fails under <c>sharpyc run</c> is invisible to the
/// project arm — this arm is what makes that class of divergence loud. It is the entry-point
/// instance of the front-end parity contract (#1144) and holds the line that a user-facing
/// SPY0908 is never acceptable (#1146).
/// </para>
/// <para>
/// The <c>.expected.cs</c> snapshots are deliberately not verified here: they are pinned against the
/// project arm's root namespace, so comparing them would assert that two intentionally different
/// namespaces are equal. Runtime behavior — stdout, diagnostics, warnings — is what has to match.
/// </para>
/// </remarks>
[Collection("HeavyCompilation")]
public class EntryFilePathMultiFileTests : FileBasedIntegrationTestsBase
{
    private static readonly string FixturesPathValue = FixtureDiscoveryHelper.FixturesPath;

    protected override string FixturesPath => FixturesPathValue;

    public EntryFilePathMultiFileTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// The multi-file half of the shared fixture discovery (<c>.skip</c> and <c>.features</c>
    /// sidecars honored by <see cref="FixtureDiscoveryHelper"/> exactly as for the project arm).
    /// </summary>
    public static IEnumerable<object[]> GetMultiFileFixtures()
    {
        foreach (var fixture in FixtureDiscoveryHelper.DiscoverFixtures(FixturesPathValue))
        {
            if (fixture.IsMultiFile)
            {
                yield return new object[] { fixture.TestName, fixture.SpyFilePath };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetMultiFileFixtures))]
    public void RunMultiFileFixtureThroughEntryFilePath(string testName, string projectDir)
    {
        Output.WriteLine($"Running test: {testName}");
        Output.WriteLine($"Project dir: {projectDir}");

        var entryPointFile = FindEntryPoint(projectDir);
        var entryFilePath = Path.Combine(projectDir, entryPointFile);
        Output.WriteLine($"Entry point (compiled as `sharpyc run` would): {entryFilePath}");

        var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories);
        Output.WriteLine("=== Source Files ===");
        foreach (var sourceFile in sourceFiles)
        {
            Output.WriteLine($"--- {Path.GetRelativePath(projectDir, sourceFile)} ---");
            Output.WriteLine(File.ReadAllText(sourceFile));
        }
        Output.WriteLine("====================");

        var features = ReadFixtureFeatures(MultiFileSidecar(projectDir, ".features"));
        var result = CompileAndExecuteEntryFile(entryFilePath, features: features);

        AssertFixtureOutcome(
            result,
            MultiFileSidecar(projectDir, ".error"),
            MultiFileSidecar(projectDir, ".expected"),
            MultiFileSidecar(projectDir, ".runtime-error"),
            MultiFileSidecar(projectDir, ".expected.cs"),
            File.ReadAllText(entryFilePath),
            verifyCSharpSnapshot: false);
    }

    /// <summary>
    /// Guards against silent discovery drift: this arm must cover every multi-file fixture the
    /// shared discovery reports, not a subset that quietly shrinks when a fixture is renamed or a
    /// discovery predicate changes (the gap-discovery convention — a shrinking sweep must fail, not
    /// pass quietly).
    /// </summary>
    [Fact]
    public void EntryFilePathArm_CoversEveryDiscoveredMultiFileFixture()
    {
        var discovered = FixtureDiscoveryHelper.DiscoverFixtures(FixturesPathValue)
            .Count(f => f.IsMultiFile);
        var armCases = GetMultiFileFixtures().Count();

        Output.WriteLine($"Discovered multi-file fixtures: {discovered}; arm theory cases: {armCases}");

        Assert.True(discovered > 0,
            $"No multi-file fixtures discovered under {FixturesPathValue}; the arm would vacuously pass.");
        Assert.Equal(discovered, armCases);
    }
}
