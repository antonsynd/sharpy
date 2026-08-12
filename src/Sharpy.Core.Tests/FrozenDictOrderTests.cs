using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy;

namespace Sharpy.Core.Tests;

/// <summary>
/// <c>frozendict</c> iterates in insertion order, like <c>dict</c>, and compares WITHOUT reading
/// that order (#1392).
/// </summary>
/// <remarks>
/// <para>
/// Before this, <see cref="FrozenDict{TKey, TValue}"/> was backed by an
/// <c>ImmutableDictionary</c> alone — a hash-array-mapped trie with no defined enumeration order —
/// and .NET randomizes string hash seeds per process. The same program printed a two-key
/// frozendict in a different order run to run (measured: 3 of 6 runs each way), so no fixture
/// could pin a multi-key <c>repr</c>.
/// </para>
/// <para>
/// THE TWO HALVES ARE ONE CONTRACT AND MUST BE TESTED TOGETHER. Making iteration ordered is only
/// safe if equality and hashing stay order-INSENSITIVE: a frozendict exists to be usable as a dict
/// key or a set element, and folding insertion order into <c>GetHashCode</c> would send two equal
/// frozendicts to different buckets. The order tests below would still pass if someone did that,
/// which is exactly why the equality tests sit beside them.
/// </para>
/// <para>
/// Every expected ordering was verified against CPython's <c>dict</c> (python3 3.12), not assumed —
/// including the two that are easy to get wrong: a repeated key keeps its FIRST position while
/// taking the LAST value, and a key present on both sides of <c>|</c> keeps its LEFT position while
/// taking the RIGHT value.
/// </para>
/// </remarks>
public class FrozenDictOrderTests
{
    private static FrozenDict<string, int> Of(params (string Key, int Value)[] pairs)
        => new FrozenDict<string, int>(
            pairs.Select(p => new KeyValuePair<string, int>(p.Key, p.Value)));

    // ===== Iteration order =====

    [Fact]
    public void Keys_AreInInsertionOrder_NotHashOrder()
    {
        // "b" before "a" is the discriminating part: sorted or hash order would not produce it.
        var fd = Of(("b", 2), ("a", 1), ("c", 3));

        fd.Keys().Should().Equal("b", "a", "c");
    }

    [Fact]
    public void ValuesAndItems_FollowTheSameOrderAsKeys()
    {
        var fd = Of(("b", 2), ("a", 1), ("c", 3));

        fd.Values().Should().Equal(2, 1, 3);
        fd.Items().Should().Equal(("b", 2), ("a", 1), ("c", 3));
    }

    [Fact]
    public void Enumerator_YieldsKeysInInsertionOrder()
    {
        var fd = Of(("b", 2), ("a", 1), ("c", 3));

        // `foreach` binds the PUBLIC GetEnumerator (pattern-based, preferred over the interface
        // one), which yields KEYS — Python's semantics for iterating a mapping. Going through LINQ
        // instead would bind IEnumerable<KeyValuePair<K,V>> and test the other enumerator; that one
        // is covered by IReadOnlyDictionary_KeysAndValues_AreInInsertionOrder below.
        var seen = new List<string>();
        foreach (var key in fd)
        {
            seen.Add(key);
        }

        seen.Should().Equal("b", "a", "c");
    }

    [Fact]
    public void IReadOnlyDictionary_KeysAndValues_AreInInsertionOrder()
    {
        // The explicit interface implementations are a separate pair of members from Keys()/Values()
        // and were a separate pair of `_dict` reads; an ordering fix that missed them would leave
        // .NET consumers seeing trie order.
        IReadOnlyDictionary<string, int> fd = Of(("b", 2), ("a", 1), ("c", 3));

        fd.Keys.Should().Equal("b", "a", "c");
        fd.Values.Should().Equal(2, 1, 3);
        fd.Select(kv => kv.Key).Should().Equal("b", "a", "c");
    }

    [Fact]
    public void ToString_RendersInInsertionOrder()
    {
        Of(("b", 2), ("a", 1)).ToString().Should().Be("frozendict({'b': 2, 'a': 1})");
    }

    [Fact]
    public void RepeatedKey_KeepsFirstPosition_AndLastValue()
    {
        // CPython: dict([('a',1),('b',2),('a',3)]) == {'a': 3, 'b': 2} — 'a' stays FIRST.
        // Appending on every write would put 'a' last and duplicate it in the key array.
        var fd = Of(("a", 1), ("b", 2), ("a", 3));

        fd.Keys().Should().Equal("a", "b");
        fd["a"].Should().Be(3);
        fd.Count.Should().Be(2);
    }

    [Fact]
    public void Order_IsIdenticalAcrossManyIndependentlyBuiltInstances()
    {
        // The defect was cross-PROCESS nondeterminism, which a single-process test cannot reproduce
        // directly. What it can assert is the property that made it possible: order must be a
        // function of insertion alone, so independently built equal-content instances agree.
        var first = Of(("alpha", 1), ("beta", 2), ("gamma", 3), ("delta", 4));

        for (int i = 0; i < 50; i++)
        {
            Of(("alpha", 1), ("beta", 2), ("gamma", 3), ("delta", 4))
                .Keys().Should().Equal(first.Keys());
        }
    }

    // ===== Union order =====

    [Fact]
    public void Union_KeepsLeftPosition_AndTakesRightValue()
    {
        // CPython: {'a':1,'b':2} | {'b':9,'c':3} == {'a': 1, 'b': 9, 'c': 3}
        var merged = Of(("a", 1), ("b", 2)) | Of(("b", 9), ("c", 3));

        merged.Keys().Should().Equal("a", "b", "c");
        merged.Values().Should().Equal(1, 9, 3);
    }

    [Fact]
    public void Union_WithDict_FollowsTheSameOrderRule()
    {
        var right = new Dict<string, int>();
        right["b"] = 9;
        right["c"] = 3;

        var merged = Of(("a", 1), ("b", 2)) | right;

        merged.Keys().Should().Equal("a", "b", "c");
        merged.Values().Should().Equal(1, 9, 3);
    }

    // ===== Equality and hashing stay order-INSENSITIVE =====

    [Fact]
    public void SamePairsInDifferentOrders_AreEqual_AndHashAlike()
    {
        var forward = Of(("a", 1), ("b", 2), ("c", 3));
        var backward = Of(("c", 3), ("b", 2), ("a", 1));

        forward.Keys().Should().NotEqual(backward.Keys());     // the orders really do differ
        forward.Equals(backward).Should().BeTrue();
        (forward == backward).Should().BeTrue();
        forward.GetHashCode().Should().Be(backward.GetHashCode());
    }

    [Fact]
    public void ReorderedFrozenDict_StillFindsItsValue_AsADictKey()
    {
        // The reason equality must ignore order: a frozendict is meant to be a key. If GetHashCode
        // folded the key array in, this lookup would miss.
        var map = new Dictionary<FrozenDict<string, int>, string>
        {
            [Of(("a", 1), ("b", 2))] = "found",
        };

        map.TryGetValue(Of(("b", 2), ("a", 1)), out var hit).Should().BeTrue();
        hit.Should().Be("found");
    }

    [Fact]
    public void ReorderedFrozenDict_CollapsesToOneSetElement()
    {
        var set = new HashSet<FrozenDict<string, int>>
        {
            Of(("a", 1), ("b", 2)),
            Of(("b", 2), ("a", 1)),
        };

        set.Should().HaveCount(1);
    }

    [Fact]
    public void EqualFrozenDicts_MayStillRenderDifferently()
    {
        // repr reads order, == does not. Stating the split explicitly so neither half is later
        // "fixed" into agreeing with the other.
        var forward = Of(("a", 1), ("b", 2));
        var backward = Of(("b", 2), ("a", 1));

        forward.Equals(backward).Should().BeTrue();
        forward.ToString().Should().NotBe(backward.ToString());
    }
}
