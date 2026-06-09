using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class IDict_Tests
{
    private static IDict CreateDict()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;
        return dict;
    }

    [Fact]
    public void Indexer_Get_ReturnsBoxedValue()
    {
        IDict d = CreateDict();
        d["a"].Should().Be(1);
    }

    [Fact]
    public void Indexer_Get_MissingKey_ThrowsKeyError()
    {
        IDict d = CreateDict();
        var act = () => d["missing"];
        act.Should().Throw<KeyError>();
    }

    [Fact]
    public void Indexer_Get_WrongTypedKey_ThrowsKeyError()
    {
        IDict d = CreateDict();
        var act = () => d[42];
        act.Should().Throw<KeyError>();
    }

    [Fact]
    public void Indexer_Set_UpdatesValue()
    {
        var original = new Dict<string, int>();
        original["a"] = 1;
        IDict d = original;
        d["a"] = 99;
        original["a"].Should().Be(99);
    }

    [Fact]
    public void Items_YieldsBoxedTuples()
    {
        IDict d = CreateDict();
        var items = d.Items().ToList();
        items.Should().Contain(("a", (object?)1));
        items.Should().Contain(("b", (object?)2));
        items.Should().HaveCount(3);
    }

    [Fact]
    public void Keys_YieldsBoxedKeys()
    {
        IDict d = CreateDict();
        d.Keys().Should().BeEquivalentTo(new object[] { "a", "b", "c" });
    }

    [Fact]
    public void Values_YieldsBoxedValues()
    {
        IDict d = CreateDict();
        d.Values().Should().BeEquivalentTo(new object[] { 1, 2, 3 });
    }

    [Fact]
    public void Contains_ReturnsTrueForPresentKey()
    {
        IDict d = CreateDict();
        d.Contains("a").Should().BeTrue();
    }

    [Fact]
    public void Contains_ReturnsFalseForMissingKey()
    {
        IDict d = CreateDict();
        d.Contains("z").Should().BeFalse();
    }

    [Fact]
    public void Contains_ReturnsFalseForWrongTypedKey()
    {
        IDict d = CreateDict();
        d.Contains(42).Should().BeFalse();
    }

    [Fact]
    public void Get_SingleArg_ReturnsSomeForPresentKey()
    {
        IDict d = CreateDict();
        var result = d.Get("a");
        result.IsSome.Should().BeTrue();
        result.Unwrap().Should().Be(1);
    }

    [Fact]
    public void Get_SingleArg_ReturnsNoneForMissingKey()
    {
        IDict d = CreateDict();
        d.Get("z").IsSome.Should().BeFalse();
    }

    [Fact]
    public void Get_SingleArg_ReturnsNoneForWrongTypedKey()
    {
        IDict d = CreateDict();
        d.Get(42).IsSome.Should().BeFalse();
    }

    [Fact]
    public void Get_WithDefault_ReturnsValueForPresentKey()
    {
        IDict d = CreateDict();
        d.Get("a", -1).Should().Be(1);
    }

    [Fact]
    public void Get_WithDefault_ReturnsDefaultForMissingKey()
    {
        IDict d = CreateDict();
        d.Get("z", -1).Should().Be(-1);
    }

    [Fact]
    public void Pop_RemovesAndReturnsValue()
    {
        IDict d = CreateDict();
        d.Pop("a").Should().Be(1);
        d.Count.Should().Be(2);
    }

    [Fact]
    public void Pop_MissingKey_ThrowsKeyError()
    {
        IDict d = CreateDict();
        var act = () => d.Pop("z");
        act.Should().Throw<KeyError>();
    }

    [Fact]
    public void Pop_WrongTypedKey_ThrowsKeyError()
    {
        IDict d = CreateDict();
        var act = () => d.Pop(42);
        act.Should().Throw<KeyError>();
    }

    [Fact]
    public void Pop_WithDefault_ReturnsDefaultForMissing()
    {
        IDict d = CreateDict();
        d.Pop("z", -1).Should().Be(-1);
    }

    [Fact]
    public void PopItem_RemovesAndReturnsPair()
    {
        IDict d = CreateDict();
        var (k, v) = d.PopItem();
        k.Should().NotBeNull();
        v.Should().NotBeNull();
        d.Count.Should().Be(2);
    }

    [Fact]
    public void SetDefault_ReturnsExistingValue()
    {
        IDict d = CreateDict();
        d.SetDefault("a", 99).Should().Be(1);
    }

    [Fact]
    public void SetDefault_InsertsNewKey()
    {
        IDict d = CreateDict();
        d.SetDefault("z", 99).Should().Be(99);
        d["z"].Should().Be(99);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        IDict d = CreateDict();
        d.Clear();
        d.Count.Should().Be(0);
    }

    [Fact]
    public void Copy_ReturnsShallowCopy()
    {
        IDict d = CreateDict();
        IDict copy = d.Copy();
        copy.Count.Should().Be(3);
        copy["a"].Should().Be(1);
    }

    [Fact]
    public void Update_MergesFromOtherIDict()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        IDict id1 = d1;

        var d2 = new Dict<string, int>();
        d2["b"] = 2;
        d2["a"] = 99;
        IDict id2 = d2;

        id1.Update(id2);
        id1["a"].Should().Be(99);
        id1["b"].Should().Be(2);
    }

    [Fact]
    public void Update_MergesFromTuples()
    {
        var d = new Dict<string, int>();
        d["a"] = 1;
        IDict id = d;

        id.Update(new List<(object?, object?)> { ("b", 2), ("c", 3) });
        id.Count.Should().Be(3);
    }

    [Fact]
    public void Remove_RemovesKey()
    {
        IDict d = CreateDict();
        d.Remove("a");
        d.Count.Should().Be(2);
    }

    [Fact]
    public void Remove_MissingKey_ThrowsKeyError()
    {
        IDict d = CreateDict();
        var act = () => d.Remove("z");
        act.Should().Throw<KeyError>();
    }

    [Fact]
    public void Remove_WrongTypedKey_ThrowsKeyError()
    {
        IDict d = CreateDict();
        var act = () => d.Remove(42);
        act.Should().Throw<KeyError>();
    }

    [Fact]
    public void ForEach_EnumeratesKeys()
    {
        IDict d = CreateDict();
        var keys = new List<object>();
        foreach (object key in d)
        {
            keys.Add(key);
        }
        keys.Should().BeEquivalentTo(new object[] { "a", "b", "c" });
    }

    [Fact]
    public void Aliasing_MutationThroughIDict_VisibleViaOriginal()
    {
        var original = new Dict<string, int>();
        original["a"] = 1;
        IDict d = original;
        d["a"] = 42;
        d["b"] = 2;
        original["a"].Should().Be(42);
        original["b"].Should().Be(2);
    }

    [Fact]
    public void Count_ReturnsSizeViaISized()
    {
        IDict d = CreateDict();
        d.Count.Should().Be(3);
    }

    [Fact]
    public void EmptyDict_MatchesIDict()
    {
        IDict d = new Dict<string, int>();
        d.Count.Should().Be(0);
        d.Keys().Should().BeEmpty();
    }
}
