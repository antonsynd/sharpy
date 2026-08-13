using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #1484: what counts as corpus. <see cref="FixtureDiscoveryHelper"/> walks the fixture tree with
/// <c>SearchOption.AllDirectories</c>, and that tree also holds things a run PUT there — a
/// multi-file fixture is compiled in place, so <c>bin</c>/<c>obj</c> appear beside its sources, and
/// a compilation that crashes writes a bundle under <c>&lt;output&gt;/.sharpy-crash/&lt;stamp&gt;/</c>
/// that includes copies of the sources it was compiling.
///
/// <para>
/// Discovered as fixtures, those copies fail as "Missing expected output file": a red suite caused
/// by an earlier run rather than by the change under test, and one that only appears after a crash
/// — so it looks like a flake and is diagnosed as one. Five such phantom fixtures were live in
/// <c>Sharpy.Stdlib.Tests</c> when this guard was written.
/// </para>
/// </summary>
public class FixtureCorpusBoundaryTests : IDisposable
{
    private readonly string _root;

    public FixtureCorpusBoundaryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sharpy_corpus_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Theory]
    [InlineData("bin/Debug/net10.0/.sharpy-crash/20260812-074819-357/sources")]
    [InlineData("bin/Debug/net10.0")]
    [InlineData("obj/Debug")]
    [InlineData("nested/fixture/bin")]
    public void Discovery_SkipsSpyFilesUnderBuildOutputAndCrashBundles(string outputRelativePath)
    {
        WriteFixture("real_fixture", "def main() -> None:\n    print(1)\n", expected: "1\n");

        var outputDir = Path.Combine(_root, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "leftover.spy"), "def main() -> None:\n    print(2)\n");

        var names = FixtureDiscoveryHelper.DiscoverFixtures(_root).Select(f => f.TestName).ToArray();

        // The positive half matters as much as the negative: without it this passes for a
        // discovery that found nothing at all.
        names.Should().Contain("real_fixture", "a fixture in the corpus proper must still be found");
        names.Should().NotContain(
            n => n.Contains("leftover", StringComparison.Ordinal),
            "a .spy file under build output or a crash bundle is not corpus");
    }

    [Fact]
    public void Discovery_CountsOnlyCorpusSourcesWhenDecidingIfAFixtureIsMultiFile()
    {
        // A single-file fixture whose own build output holds a second .spy must stay single-file.
        // The multi-file decision counts *.spy recursively, so unexcluded output would flip it —
        // and a fixture wrongly treated as multi-file looks for main.expected instead of its own.
        var dir = Path.Combine(_root, "single");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "main.spy"), "def main() -> None:\n    print(1)\n");
        File.WriteAllText(Path.Combine(dir, "main.expected"), "1\n");

        var stale = Path.Combine(dir, "obj", "Debug");
        Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale, "main.spy"), "def main() -> None:\n    print(2)\n");

        var fixtures = FixtureDiscoveryHelper.DiscoverFixtures(_root).ToArray();

        fixtures.Should().ContainSingle();
        fixtures[0].IsMultiFile.Should().BeFalse(
            "the second main.spy is build output, so only one corpus source exists");
    }

    private void WriteFixture(string name, string source, string expected)
    {
        File.WriteAllText(Path.Combine(_root, name + ".spy"), source);
        File.WriteAllText(Path.Combine(_root, name + ".expected"), expected);
    }
}
