using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy;

namespace Sharpy.Core.Tests;

public class FrozenDictTests
{
    // ===== Construction =====

    [Fact]
    public void FrozenDict_Empty_HasCountZero()
    {
        var fd = new FrozenDict<string, int>();
        fd.Count.Should().Be(0);
    }

    [Fact]
    public void FrozenDict_FromDict_CopiesAllPairs()
    {
        var d = new Dict<string, int>();
        d["a"] = 1;
        d["b"] = 2;

        var fd = new FrozenDict<string, int>(d);

        fd.Count.Should().Be(2);
        fd["a"].Should().Be(1);
        fd["b"].Should().Be(2);
    }

    [Fact]
    public void FrozenDict_FromPairs_CopiesAllPairs()
    {
        var pairs = new[]
        {
            new KeyValuePair<string, int>("x", 10),
            new KeyValuePair<string, int>("y", 20),
        };

        var fd = new FrozenDict<string, int>(pairs);

        fd.Count.Should().Be(2);
        fd["x"].Should().Be(10);
        fd["y"].Should().Be(20);
    }

    [Fact]
    public void FrozenDict_FromPairs_DuplicateKeys_LastWins()
    {
        var pairs = new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("a", 99),
        };

        var fd = new FrozenDict<string, int>(pairs);

        fd.Count.Should().Be(1);
        fd["a"].Should().Be(99);
    }

    [Fact]
    public void FrozenDict_FromNull_ThrowsTypeError()
    {
        var act = () => new FrozenDict<string, int>((IEnumerable<KeyValuePair<string, int>>)null!);
        act.Should().Throw<TypeError>();

        var act2 = () => new FrozenDict<string, int>((Dict<string, int>)null!);
        act2.Should().Throw<TypeError>();
    }

    // ===== Indexer / KeyError =====

    [Fact]
    public void FrozenDict_Indexer_ReturnsValueForExistingKey()
    {
        var fd = new FrozenDict<string, int>(new[] { new KeyValuePair<string, int>("k", 42) });
        fd["k"].Should().Be(42);
    }

    [Fact]
    public void FrozenDict_Indexer_MissingKey_ThrowsKeyError()
    {
        var fd = new FrozenDict<string, int>();
        var act = () => _ = fd["missing"];
        act.Should().Throw<KeyError>();
    }

    // ===== ContainsKey / Contains =====

    [Fact]
    public void FrozenDict_ContainsKey_Works()
    {
        var fd = new FrozenDict<string, int>(new[] { new KeyValuePair<string, int>("a", 1) });
        fd.ContainsKey("a").Should().BeTrue();
        fd.ContainsKey("b").Should().BeFalse();
    }

    [Fact]
    public void FrozenDict_Contains_MatchesContainsKey()
    {
        var fd = new FrozenDict<string, int>(new[] { new KeyValuePair<string, int>("a", 1) });
        fd.Contains("a").Should().BeTrue();
        fd.Contains("b").Should().BeFalse();
    }

    // ===== Get with default =====

    [Fact]
    public void FrozenDict_Get_ReturnsValue_ForExistingKey()
    {
        var fd = new FrozenDict<string, int>(new[] { new KeyValuePair<string, int>("a", 1) });
        fd.Get("a", 0).Should().Be(1);
    }

    [Fact]
    public void FrozenDict_Get_ReturnsDefault_ForMissingKey()
    {
        var fd = new FrozenDict<string, int>();
        fd.Get("missing", 42).Should().Be(42);
    }

    [Fact]
    public void FrozenDict_Get_WithoutDefault_ReturnsDefaultT()
    {
        var fd = new FrozenDict<string, int>();
        fd.Get("missing").Should().Be(0);
    }

    // ===== Keys / Values / Items =====

    [Fact]
    public void FrozenDict_Keys_ReturnsAllKeys()
    {
        var fd = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
        });

        fd.Keys.Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void FrozenDict_Values_ReturnsAllValues()
    {
        var fd = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
        });

        fd.Values.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void FrozenDict_Items_ReturnsAllTuples()
    {
        var fd = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
        });

        fd.Items().Should().BeEquivalentTo(new[] { ("a", 1), ("b", 2) });
    }

    // ===== Count =====

    [Fact]
    public void FrozenDict_Count_ReflectsNumberOfPairs()
    {
        var fd = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("c", 3),
        });

        fd.Count.Should().Be(3);
    }

    // ===== Iteration =====

    [Fact]
    public void FrozenDict_IteratesOverKeys_PythonSemantics()
    {
        var fd = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
        });

        var keys = new List<string>();

        foreach (var k in fd)
        {
            keys.Add(k);
        }

        keys.Should().BeEquivalentTo(new[] { "a", "b" });
    }

    // ===== Copy =====

    [Fact]
    public void FrozenDict_Copy_ReturnsSameInstance()
    {
        var fd = new FrozenDict<string, int>(new[] { new KeyValuePair<string, int>("a", 1) });
        var copy = fd.Copy();
        ReferenceEquals(fd, copy).Should().BeTrue();
    }

    // ===== Union (__or__) =====

    [Fact]
    public void FrozenDict_Or_ProducesUnion_WithRightKeysOverwriting()
    {
        var a = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("x", 1),
            new KeyValuePair<string, int>("y", 2),
        });

        var b = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("y", 99),
            new KeyValuePair<string, int>("z", 3),
        });

        var merged = a | b;

        merged.Count.Should().Be(3);
        merged["x"].Should().Be(1);
        merged["y"].Should().Be(99);
        merged["z"].Should().Be(3);

        // Originals unchanged
        a["y"].Should().Be(2);
        b["y"].Should().Be(99);
    }

    // ===== Equality =====

    [Fact]
    public void FrozenDict_Equals_SameContent_DifferentOrder()
    {
        var a = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
        });

        var b = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("a", 1),
        });

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void FrozenDict_Equals_DifferentContent_ReturnsFalse()
    {
        var a = new FrozenDict<string, int>(new[] { new KeyValuePair<string, int>("a", 1) });
        var b = new FrozenDict<string, int>(new[] { new KeyValuePair<string, int>("a", 2) });

        a.Equals(b).Should().BeFalse();
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void FrozenDict_Equals_Null_ReturnsFalse()
    {
        var fd = new FrozenDict<string, int>();
        fd.Equals((FrozenDict<string, int>?)null).Should().BeFalse();
        fd.Equals((object?)null).Should().BeFalse();
    }

    [Fact]
    public void FrozenDict_GetHashCode_ConsistentWithEquals()
    {
        var a = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
        });

        var b = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("a", 1),
        });

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    // ===== ToString / __repr__ =====

    [Fact]
    public void FrozenDict_ToString_Empty_ReturnsFrozendictBraces()
    {
        var fd = new FrozenDict<string, int>();
        fd.ToString().Should().Be("frozendict({})");
    }

    [Fact]
    public void FrozenDict_ToString_NonEmpty_UsesReprFormat()
    {
        var fd = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
        });

        fd.ToString().Should().Be("frozendict({'a': 1})");
    }

    // ===== ISized / len() dispatch =====

    [Fact]
    public void FrozenDict_ImplementsISized_ForLenDispatch()
    {
        ISized fd = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
        });

        fd.Count.Should().Be(2);
    }

    // ===== Immutability: compile-time check that no mutation methods exist =====

    [Fact]
    public void FrozenDict_HasNoMutationMethods()
    {
        var type = typeof(FrozenDict<string, int>);
        var mutatingNames = new[] { "Add", "Remove", "Clear", "Update", "Pop", "PopItem", "SetDefault" };

        foreach (var name in mutatingNames)
        {
            // Allow only non-public (e.g. explicit interface impls would be non-public),
            // but FrozenDict has none at all.
            var publicMethod = type.GetMethod(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            publicMethod.Should().BeNull(
                because: $"FrozenDict must not expose mutation method {name}");
        }
    }

    [Fact]
    public void FrozenDict_Indexer_IsReadOnly()
    {
        // The indexer has a getter but no setter; verify via reflection.
        var type = typeof(FrozenDict<string, int>);
        var indexer = type.GetProperty("Item");
        indexer.Should().NotBeNull();
        indexer!.CanRead.Should().BeTrue();
        indexer.CanWrite.Should().BeFalse();
    }

    // ===== IReadOnlyDictionary contract =====

    [Fact]
    public void FrozenDict_IsIReadOnlyDictionary()
    {
        IReadOnlyDictionary<string, int> fd = new FrozenDict<string, int>(new[]
        {
            new KeyValuePair<string, int>("k", 1),
        });

        fd.Count.Should().Be(1);
        fd.ContainsKey("k").Should().BeTrue();
        fd["k"].Should().Be(1);
        fd.Keys.Should().BeEquivalentTo(new[] { "k" });
        fd.Values.Should().BeEquivalentTo(new[] { 1 });
        fd.TryGetValue("k", out var v).Should().BeTrue();
        v.Should().Be(1);
    }
}
