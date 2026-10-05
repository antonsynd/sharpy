using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// #2179: the LetEmissionInvarianceTests corpus Theory runs as K shard classes
/// (Sharpy.Compiler.Tests.Conformance.LetEmissionInvarianceTests.Shard0..Shard7), by the same cure and
/// the same shard function as the fixture corpus (<see cref="FixtureShardTotalityTests"/>).
/// The partition must lose no fixture, run none twice and leave no class empty, and K is pinned
/// here as a LITERAL — not read from <c>Instrument.ShardCount</c>, which the shards themselves use,
/// so a count derived from it would agree with them by construction. The unsharded side is the instrument's
/// fresh-walk <c>ComparableFixtureNames</c>, not the shards' cache.
/// </summary>
public class LetEmissionShardTotalityTests
{
    private const int ExpectedShardClasses = 8;

    private static IReadOnlyList<FixtureShardScan.Shard> Shards() => FixtureShardScan.ReadShards(typeof(Conformance.LetEmissionInvarianceTests.LetTwinShard));

    private static string[] Unsharded()
        => Conformance.LetEmissionInvarianceTests.Instrument.ComparableFixtureNames().Select(r => (string)r[0]).ToArray();

    [Fact]
    public void ShardClassCount_IsTheLiteralK()
    {
        Assert.Equal(ExpectedShardClasses, Shards().Count);
    }

    [Fact]
    public void ShardUnion_EqualsTheUnshardedCorpus_AsAMultiset()
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
}
