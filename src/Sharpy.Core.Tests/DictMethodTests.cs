using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class DictMethodTests
{
    // ===== Contains (method alias for ContainsKey) =====

    [Fact]
    public void Contains_ExistingKey_ReturnsTrue()
    {
        var dict = new Dict<string, int>();
        dict["x"] = 42;
        dict.Contains("x").Should().BeTrue();
    }

    [Fact]
    public void Contains_MissingKey_ReturnsFalse()
    {
        var dict = new Dict<string, int>();
        dict.Contains("missing").Should().BeFalse();
    }

    // ===== Update(IEnumerable<(K,V)>) — tuple overload not in DictTests.cs =====

    [Fact]
    public void Update_FromTupleSequence_AddsNewPairs()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;

        dict.Update(new[] { ("b", 2), ("c", 3) });

        dict["a"].Should().Be(1);
        dict["b"].Should().Be(2);
        dict["c"].Should().Be(3);
        dict.Count.Should().Be(3);
    }

    [Fact]
    public void Update_FromTupleSequence_OverwritesExistingKeys()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;

        dict.Update(new[] { ("a", 99), ("b", 2) });

        dict["a"].Should().Be(99);
        dict["b"].Should().Be(2);
        dict.Count.Should().Be(2);
    }

    [Fact]
    public void Update_FromEmptyTupleSequence_NoChange()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;

        dict.Update(Array.Empty<(string, int)>());

        dict.Count.Should().Be(1);
        dict["a"].Should().Be(1);
    }

    // ===== Merge — creates new dict without mutating originals =====

    [Fact]
    public void Merge_ReturnsCombinedDict()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        d1["b"] = 2;

        var d2 = new Dict<string, int>();
        d2["b"] = 20;
        d2["c"] = 3;

        var result = d1.Merge(d2);

        result["a"].Should().Be(1);
        result["b"].Should().Be(20); // d2 wins
        result["c"].Should().Be(3);
        result.Count.Should().Be(3);
    }

    [Fact]
    public void Merge_DoesNotMutateOriginals()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;

        var d2 = new Dict<string, int>();
        d2["b"] = 2;

        var result = d1.Merge(d2);

        // Originals unchanged
        d1.Count.Should().Be(1);
        d1.ContainsKey("b").Should().BeFalse();
        d2.Count.Should().Be(1);
        d2.ContainsKey("a").Should().BeFalse();

        // Mutate result — should not affect originals
        result["c"] = 3;
        d1.ContainsKey("c").Should().BeFalse();
        d2.ContainsKey("c").Should().BeFalse();
    }

    [Fact]
    public void Merge_WithEmptyDict_ReturnsEquivalentCopy()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;

        var result = d1.Merge(new Dict<string, int>());

        result.Count.Should().Be(1);
        result["a"].Should().Be(1);
    }

    // ===== Remove — throws KeyError on missing key =====

    [Fact]
    public void Remove_ExistingKey_RemovesIt()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        dict["b"] = 2;

        dict.Remove("a");

        dict.ContainsKey("a").Should().BeFalse();
        dict.Count.Should().Be(1);
    }

    [Fact]
    public void Remove_MissingKey_ThrowsKeyError()
    {
        var dict = new Dict<string, int>();

        dict.Invoking(d => d.Remove("missing"))
            .Should().Throw<KeyError>();
    }

    // ===== ToDictionary =====

    [Fact]
    public void ToDictionary_ReturnsDotNetDictionary()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        dict["b"] = 2;

        var netDict = dict.ToDictionary();

        netDict.Should().BeOfType<System.Collections.Generic.Dictionary<string, int>>();
        netDict.Should().HaveCount(2);
        netDict["a"].Should().Be(1);
        netDict["b"].Should().Be(2);
    }

    [Fact]
    public void ToDictionary_MutatingResult_DoesNotAffectOriginal()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;

        var netDict = dict.ToDictionary();
        netDict["b"] = 99;

        dict.ContainsKey("b").Should().BeFalse();
    }

    // ===== Insertion order preserved in iteration =====

    [Fact]
    public void Iteration_PreservesInsertionOrder()
    {
        var dict = new Dict<string, int>();
        dict["first"] = 1;
        dict["second"] = 2;
        dict["third"] = 3;

        var keys = new List<string>();
        foreach (var key in dict)
        {
            keys.Add(key);
        }

        keys[0].Should().Be("first");
        keys[1].Should().Be("second");
        keys[2].Should().Be("third");
    }

    [Fact]
    public void Iteration_AfterUpdate_NewKeyAtEnd()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        dict["b"] = 2;

        dict.Update(new Dict<string, int> { ["c"] = 3 });

        var keys = new List<string>();
        foreach (var key in dict)
        {
            keys.Add(key);
        }

        keys[0].Should().Be("a");
        keys[1].Should().Be("b");
        keys[2].Should().Be("c");
    }

    // ===== PopItem — last:true variant not in DictTests.cs =====

    [Fact]
    public void PopItem_LastTrue_RemovesAndReturnsLastItem()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;

        var (key, value) = dict.PopItem(last: true);

        key.Should().Be("c");
        value.Should().Be(3);
        dict.Count.Should().Be(2);
        dict.ContainsKey("c").Should().BeFalse();
    }

    [Fact]
    public void PopItem_LastFalse_RemovesAndReturnsFirstItem()
    {
        var dict = new Dict<string, int>();
        dict["x"] = 10;
        dict["y"] = 20;

        var (key, value) = dict.PopItem(last: false);

        key.Should().Be("x");
        value.Should().Be(10);
        dict.Count.Should().Be(1);
    }

    // ===== TryGetValue =====

    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrueAndValue()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 42;

        bool found = dict.TryGetValue("a", out var value);

        found.Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public void TryGetValue_MissingKey_ReturnsFalse()
    {
        var dict = new Dict<string, int>();

        bool found = dict.TryGetValue("missing", out var value);

        found.Should().BeFalse();
        value.Should().Be(default(int));
    }
}
