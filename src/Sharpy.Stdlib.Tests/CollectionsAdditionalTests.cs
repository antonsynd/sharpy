using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class OrderedDict_Tests
{
    [Fact]
    public void OrderedDict_MaintainsInsertionOrder()
    {
        var od = new OrderedDict<string, int>();
        od["c"] = 3;
        od["a"] = 1;
        od["b"] = 2;

        od.Keys().Should().Equal("c", "a", "b");
        od.Values().Should().Equal(3, 1, 2);
    }

    [Fact]
    public void OrderedDict_UpdateExistingKey_PreservesOrder()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;
        od["b"] = 99;

        od.Keys().Should().Equal("a", "b", "c");
        od["b"].Should().Be(99);
    }

    [Fact]
    public void OrderedDict_MoveToEnd_Last()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;

        od.MoveToEnd("a", last: true);
        od.Keys().Should().Equal("b", "c", "a");
    }

    [Fact]
    public void OrderedDict_MoveToEnd_First()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;

        od.MoveToEnd("c", last: false);
        od.Keys().Should().Equal("c", "a", "b");
    }

    [Fact]
    public void OrderedDict_MoveToEnd_MissingKey_ThrowsKeyError()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;

        FluentActions.Invoking(() => od.MoveToEnd("z"))
            .Should().Throw<KeyError>();
    }

    [Fact]
    public void OrderedDict_Popitem_Last()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;

        var (key, value) = od.Popitem(last: true);
        key.Should().Be("c");
        value.Should().Be(3);
        od.Count.Should().Be(2);
        od.Keys().Should().Equal("a", "b");
    }

    [Fact]
    public void OrderedDict_Popitem_First()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;

        var (key, value) = od.Popitem(last: false);
        key.Should().Be("a");
        value.Should().Be(1);
        od.Count.Should().Be(2);
        od.Keys().Should().Equal("b", "c");
    }

    [Fact]
    public void OrderedDict_Popitem_Empty_ThrowsKeyError()
    {
        var od = new OrderedDict<string, int>();

        FluentActions.Invoking(() => od.Popitem())
            .Should().Throw<KeyError>();
    }

    [Fact]
    public void OrderedDict_Pop_ReturnsValueAndRemoves()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;

        var val = od.Pop("a");
        val.Should().Be(1);
        od.Count.Should().Be(1);
        od.ContainsKey("a").Should().BeFalse();
    }

    [Fact]
    public void OrderedDict_Pop_MissingKey_ThrowsKeyError()
    {
        var od = new OrderedDict<string, int>();

        FluentActions.Invoking(() => od.Pop("z"))
            .Should().Throw<KeyError>();
    }

    [Fact]
    public void OrderedDict_Pop_WithDefault_ReturnDefault()
    {
        var od = new OrderedDict<string, int>();

        od.Pop("z", 42).Should().Be(42);
    }

    [Fact]
    public void OrderedDict_ContainsKey()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;

        od.ContainsKey("a").Should().BeTrue();
        od.ContainsKey("b").Should().BeFalse();
    }

    [Fact]
    public void OrderedDict_Clear()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;

        od.Clear();
        od.Count.Should().Be(0);
    }

    [Fact]
    public void OrderedDict_Copy()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;

        var copy = od.Copy();
        copy.Keys().Should().Equal("a", "b");
        copy["a"].Should().Be(1);

        // Shallow copy - modifying copy doesn't affect original
        copy["c"] = 3;
        od.ContainsKey("c").Should().BeFalse();
    }

    [Fact]
    public void OrderedDict_Get_ExistingKey()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;

        od.Get("a").Should().Be(1);
    }

    [Fact]
    public void OrderedDict_Get_MissingKey_ReturnsDefault()
    {
        var od = new OrderedDict<string, int>();

        od.Get("a", 42).Should().Be(42);
    }

    [Fact]
    public void OrderedDict_Items()
    {
        var od = new OrderedDict<string, int>();
        od["x"] = 10;
        od["y"] = 20;

        od.Items().Should().Equal(("x", 10), ("y", 20));
    }

    [Fact]
    public void OrderedDict_ConstructFromPairs()
    {
        var pairs = new[] { ("a", 1), ("b", 2), ("c", 3) };
        var od = new OrderedDict<string, int>(pairs);

        od.Keys().Should().Equal("a", "b", "c");
        od.Values().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void OrderedDict_GetMissingKey_ThrowsKeyError()
    {
        var od = new OrderedDict<string, int>();

        FluentActions.Invoking(() => { var _ = od["z"]; })
            .Should().Throw<KeyError>();
    }
}

public class ChainMap_Tests
{
    [Fact]
    public void ChainMap_LookupSearchesThroughChain()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        var d2 = new Dict<string, int>();
        d2["b"] = 2;
        d2["a"] = 99;

        var cm = new ChainMap<string, int>(d1, d2);

        // First map takes priority
        cm["a"].Should().Be(1);
        cm["b"].Should().Be(2);
    }

    [Fact]
    public void ChainMap_WritesGoToFirstMap()
    {
        var d1 = new Dict<string, int>();
        var d2 = new Dict<string, int>();
        d2["a"] = 1;

        var cm = new ChainMap<string, int>(d1, d2);
        cm["x"] = 10;

        d1.ContainsKey("x").Should().BeTrue();
        d2.ContainsKey("x").Should().BeFalse();
    }

    [Fact]
    public void ChainMap_ContainsKey_SearchesAllMaps()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        var d2 = new Dict<string, int>();
        d2["b"] = 2;

        var cm = new ChainMap<string, int>(d1, d2);

        cm.ContainsKey("a").Should().BeTrue();
        cm.ContainsKey("b").Should().BeTrue();
        cm.ContainsKey("c").Should().BeFalse();
    }

    [Fact]
    public void ChainMap_MissingKey_ThrowsKeyError()
    {
        var cm = new ChainMap<string, int>();

        FluentActions.Invoking(() => { var _ = cm["z"]; })
            .Should().Throw<KeyError>();
    }

    [Fact]
    public void ChainMap_NewChild()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        var cm = new ChainMap<string, int>(d1);

        var child = new Dict<string, int>();
        child["a"] = 99;
        child["b"] = 2;
        var childCm = cm.NewChild(child);

        childCm["a"].Should().Be(99);
        childCm["b"].Should().Be(2);
        childCm.Maps.Count.Should().Be(2);
    }

    [Fact]
    public void ChainMap_NewChild_NullCreatesEmptyDict()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        var cm = new ChainMap<string, int>(d1);

        var childCm = cm.NewChild();
        childCm.Maps.Count.Should().Be(2);
        childCm["a"].Should().Be(1);
    }

    [Fact]
    public void ChainMap_Parents()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        var d2 = new Dict<string, int>();
        d2["b"] = 2;
        var d3 = new Dict<string, int>();
        d3["c"] = 3;

        var cm = new ChainMap<string, int>(d1, d2, d3);
        var parents = cm.Parents;

        parents.Maps.Count.Should().Be(2);
        parents.ContainsKey("b").Should().BeTrue();
        parents.ContainsKey("c").Should().BeTrue();
        parents.ContainsKey("a").Should().BeFalse();
    }

    [Fact]
    public void ChainMap_Parents_SingleMap_ReturnsEmpty()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        var cm = new ChainMap<string, int>(d1);

        var parents = cm.Parents;
        parents.Maps.Count.Should().Be(1);
        parents.ContainsKey("a").Should().BeFalse();
    }

    [Fact]
    public void ChainMap_Get_WithDefault()
    {
        var cm = new ChainMap<string, int>();

        cm.Get("x", 42).Should().Be(42);
    }

    [Fact]
    public void ChainMap_Keys_ReturnsUniqueKeys()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        d1["b"] = 2;
        var d2 = new Dict<string, int>();
        d2["b"] = 20;
        d2["c"] = 30;

        var cm = new ChainMap<string, int>(d1, d2);

        var keys = new List<string>(cm.Keys());
        keys.Should().HaveCount(3);
        keys.Should().Contain("a");
        keys.Should().Contain("b");
        keys.Should().Contain("c");
    }

    [Fact]
    public void ChainMap_Count_ReturnsUniqueKeyCount()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        var d2 = new Dict<string, int>();
        d2["a"] = 10;
        d2["b"] = 20;

        var cm = new ChainMap<string, int>(d1, d2);
        cm.Count.Should().Be(2);
    }

    [Fact]
    public void ChainMap_Pop_RemovesFromFirstMap()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        var d2 = new Dict<string, int>();
        d2["a"] = 99;

        var cm = new ChainMap<string, int>(d1, d2);
        var val = cm.Pop("a");
        val.Should().Be(1);

        // Now "a" in first map is removed, so chainmap sees d2's value
        cm["a"].Should().Be(99);
    }

    [Fact]
    public void ChainMap_Clear_ClearsFirstMap()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        d1["b"] = 2;
        var d2 = new Dict<string, int>();
        d2["c"] = 3;

        var cm = new ChainMap<string, int>(d1, d2);
        cm.Clear();

        d1.Count.Should().Be(0);
        d2.Count.Should().Be(1);
        cm.ContainsKey("c").Should().BeTrue();
    }

    [Fact]
    public void ChainMap_DefaultConstructor_HasOneEmptyMap()
    {
        var cm = new ChainMap<string, int>();
        cm.Maps.Count.Should().Be(1);
        cm.Count.Should().Be(0);
    }
}
