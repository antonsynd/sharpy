using Sharpy.TestInfrastructure.Integration;
using Sharpy.Stdlib.Tests.Integration.FileBasedIntegrationTests;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// #2179: the Stdlib.Tests fixture corpus runs as K shard classes (Sharpy.Stdlib.Tests.Integration.FileBasedIntegrationTests.Shard0..Shard3).
/// The partition must lose no fixture, run none twice and leave no class empty, and K is pinned
/// here as a LITERAL — not read from <c>StdlibFixtureShard.ShardCount</c>, which the shards themselves use,
/// so a count derived from it would agree with them by construction. The unsharded side is a fresh
/// disk walk (<see cref="FileBasedIntegrationTestsBase.DiscoverTestFixtures(string)"/>), not the
/// shards' cache.
/// </summary>
public class StdlibFixtureShardTotalityTests
{
    private const int ExpectedShardClasses = 4;

    private static IReadOnlyList<FixtureShardScan.Shard> Shards() => FixtureShardScan.ReadShards(typeof(StdlibFixtureShard));

    private static string[] Unsharded()
        => FileBasedIntegrationTestsBase.DiscoverTestFixtures(FixtureRoots.StdlibTests.Path).Select(r => (string)r[0]).ToArray();

    [Fact]
    public void ShardClassCount_IsTheLiteralK()
    {
        Assert.Equal(ExpectedShardClasses, Shards().Count);
    }

    [Fact]
    public void ShardUnion_EqualsUnshardedDiscovery_AsAMultiset()
    {
        var union = Shards().SelectMany(s => s.TestNames).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var all = Unsharded().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(all);
        Assert.Equal(all, union);
    }

    [Fact]
    public void Shards_ArePairwiseDisjoint()
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var overlaps = new System.Collections.Generic.List<string>();
        foreach (var shard in Shards())
        {
            foreach (var name in shard.TestNames.Distinct(StringComparer.Ordinal))
            {
                if (owners.TryGetValue(name, out var owner))
                    overlaps.Add($"{name}: {owner} and {shard.Type.Name}");
                else
                    owners[name] = shard.Type.Name;
            }
        }

        Assert.True(overlaps.Count == 0, "fixtures in more than one shard:\n" + string.Join("\n", overlaps));
    }

    [Fact]
    public void NoShard_IsEmpty()
    {
        var empty = Shards().Where(s => s.TestNames.Count == 0).Select(s => s.Type.Name).ToArray();

        Assert.True(empty.Length == 0, "empty shard classes: " + string.Join(", ", empty));
    }

    /// <summary>
    /// The rows above are what each shard OFFERS; this is how xUnit RUNS them: every shard's
    /// <c>RunTestFixture</c> is one un-skipped <c>[Theory]</c> over its own <c>GetTestFixtures</c>, with no
    /// trait the shard base lacks — a <c>Skip</c> collapses a shard to one skipped row and a
    /// Category trait lets a CI filter drop it, both invisible to the row totality.
    /// </summary>
    [Fact]
    public void EveryShard_RunsItsRowsAsOneUnskippedUncategorisedTheory()
    {
        var (checkedClasses, violations) = FixtureShardScan.ShardMethodViolations(typeof(StdlibFixtureShard), "RunTestFixture");

        Assert.Equal(ExpectedShardClasses, checkedClasses);
        Assert.True(violations.Count == 0, "shard methods xUnit would not run as offered:\n" + string.Join("\n", violations));
    }
}
