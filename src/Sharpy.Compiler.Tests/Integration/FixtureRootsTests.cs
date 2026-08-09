using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Guards the fixture-root declarations themselves (#1338).
///
/// <para>
/// The defect this exists for: fixture discovery was anchored on the calling assembly's location,
/// so every sweep silently covered whichever corpus sat next to its host project. Two sweeps
/// meant to be corpus-wide — the differential CPython oracle and the metamorphic transform sweep —
/// never saw a single Stdlib.Tests fixture, and no test could have said so, because no test could
/// name a root.
/// </para>
///
/// <para>
/// So the invariants asserted here are about the roots, not about counts: exact fixture counts
/// churn on every new fixture and would be edited rather than believed. What must hold is that
/// each named root points where it claims, that the two corpora are disjoint, and that a
/// cross-corpus discovery is exactly their union with no name collisions.
/// </para>
/// </summary>
public class FixtureRootsTests
{
    /// <summary>
    /// A floor, not a count. Detects a root resolving somewhere plausible-but-wrong (an empty
    /// directory, a single category subdirectory) without failing every time a fixture is added.
    /// </summary>
    private const int CompilerCorpusFloor = 1_500;
    private const int StdlibCorpusFloor = 100;

    [Fact]
    public void NamedRoots_ResolveToTheDirectoriesTheyClaim()
    {
        FixtureRoots.CompilerTests.Path.Should().Be(Path.GetFullPath(Path.Combine(
            FixtureRoots.RepositoryRoot,
            "src", "Sharpy.Compiler.Tests", "Integration", "TestFixtures")));
        FixtureRoots.StdlibTests.Path.Should().Be(Path.GetFullPath(Path.Combine(
            FixtureRoots.RepositoryRoot,
            "src", "Sharpy.Stdlib.Tests", "Integration", "TestFixtures")));

        Directory.Exists(FixtureRoots.CompilerTests.Path).Should().BeTrue();
        Directory.Exists(FixtureRoots.StdlibTests.Path).Should().BeTrue();

        File.Exists(Path.Combine(FixtureRoots.RepositoryRoot, "sharpy.sln")).Should().BeTrue(
            "the repository root is what every named root is resolved against");
    }

    [Fact]
    public void NamedRoots_AreDisjointCorpora()
    {
        FixtureRoots.CompilerTests.Path.Should().NotBe(FixtureRoots.StdlibTests.Path);
        FixtureRoots.CompilerTests.Path.Should().NotStartWith(
            FixtureRoots.StdlibTests.Path + Path.DirectorySeparatorChar);
        FixtureRoots.StdlibTests.Path.Should().NotStartWith(
            FixtureRoots.CompilerTests.Path + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void EachRoot_DiscoversItsOwnCorpus_LabelledAsItself()
    {
        var compiler = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests).ToList();
        var stdlib = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.StdlibTests).ToList();

        compiler.Count.Should().BeGreaterThan(CompilerCorpusFloor);
        stdlib.Count.Should().BeGreaterThan(StdlibCorpusFloor);

        // The primary root's names carry no prefix — the drain-on-fix allowlists are keyed on them.
        compiler.Should().OnlyContain(f => f.RootLabel == "");
        compiler.Should().OnlyContain(f => !f.TestName.StartsWith("stdlib-tests/", StringComparison.Ordinal));

        stdlib.Should().OnlyContain(f => f.RootLabel == "stdlib-tests");
        stdlib.Should().OnlyContain(f => f.TestName.StartsWith("stdlib-tests/", StringComparison.Ordinal));
    }

    [Fact]
    public void CrossCorpusDiscovery_IsTheUnion_WithNoNameCollisions()
    {
        var compiler = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests).ToList();
        var stdlib = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.StdlibTests).ToList();
        var both = FixtureDiscoveryHelper.DiscoverFixturesFrom(
            FixtureRoots.CompilerTests, FixtureRoots.StdlibTests).ToList();

        both.Count.Should().Be(compiler.Count + stdlib.Count);

        var names = both.Select(f => f.TestName).ToList();
        names.Should().OnlyHaveUniqueItems(
            "a cross-corpus sweep keys its cells and allowlist entries by test name");
    }
}
