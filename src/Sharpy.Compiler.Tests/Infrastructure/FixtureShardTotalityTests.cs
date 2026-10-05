using Sharpy.TestInfrastructure.Integration;
using Sharpy.Compiler.Tests.Integration.FileBasedIntegrationTests;
using Xunit;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// #2179: the Compiler.Tests fixture corpus runs as K shard classes (Sharpy.Compiler.Tests.Integration.FileBasedIntegrationTests.Shard0..Shard7).
/// The partition must lose no fixture, run none twice and leave no class empty, and K is pinned
/// here as a LITERAL — not read from <c>CompilerFixtureShard.ShardCount</c>, which the shards themselves use,
/// so a count derived from it would agree with them by construction. The unsharded side is a fresh
/// disk walk (<see cref="FileBasedIntegrationTestsBase.DiscoverTestFixtures(string)"/>), not the
/// shards' cache.
/// </summary>
public class FixtureShardTotalityTests
{
    private const int ExpectedShardClasses = 8;

    private static IReadOnlyList<FixtureShardScan.Shard> Shards() => FixtureShardScan.ReadShards(typeof(CompilerFixtureShard));

    private static string[] Unsharded()
        => FileBasedIntegrationTestsBase.DiscoverTestFixtures(FixtureRoots.CompilerTests.Path).Select(r => (string)r[0]).ToArray();

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
    /// The shard function is 32-bit FNV-1a: "foobar" hashes to 0xbf9cf968 (the published test
    /// vector), and two fixture paths land where the reference implementation puts them. A swap to
    /// a per-process-randomised hash (string.GetHashCode) would move rows between the discovery and
    /// execution processes.
    /// </summary>
    [Theory]
    [InlineData("foobar", 1000, 720)]
    [InlineData("basics/hello_world.spy", 8, 2)]
    [InlineData("imports/pkg_multi", 8, 6)]
    [InlineData("imports/pkg_multi", 4, 2)]
    public void ShardOf_IsFnv1a(string relativePath, int shardCount, int expected)
    {
        Assert.Equal(expected, FileBasedIntegrationTestsBase.ShardOf(relativePath, shardCount));
    }
}
