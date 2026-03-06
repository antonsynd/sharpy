using Xunit;
using FluentAssertions;
using System.Linq;

namespace Sharpy.Core.Tests;

public class CollectionsAdditional_Tests
{
    // --- OrderedDict ---

    [Fact]
    public void OrderedDict_MaintainsInsertionOrder()
    {
        var od = new Sharpy.OrderedDict<string, int>();
        od["c"] = 3;
        od["a"] = 1;
        od["b"] = 2;
        od.Keys.ToList().Should().Equal("c", "a", "b");
    }

    [Fact]
    public void OrderedDict_Indexer_Get_ThrowsKeyError()
    {
        var od = new Sharpy.OrderedDict<string, int>();
        FluentActions.Invoking(() => { var x = od["missing"]; })
            .Should().Throw<Sharpy.KeyError>();
    }

    [Fact]
    public void OrderedDict_MoveToEnd_Last()
    {
        var od = new Sharpy.OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;
        od.MoveToEnd("a");
        od.Keys.ToList().Should().Equal("b", "c", "a");
    }

    [Fact]
    public void OrderedDict_MoveToEnd_First()
    {
        var od = new Sharpy.OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;
        od.MoveToEnd("c", false);
        od.Keys.ToList().Should().Equal("c", "a", "b");
    }

    [Fact]
    public void OrderedDict_Popitem_Last()
    {
        var od = new Sharpy.OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        var item = od.Popitem();
        item.Should().Be(("b", 2));
        od.Count.Should().Be(1);
    }

    [Fact]
    public void OrderedDict_Popitem_First()
    {
        var od = new Sharpy.OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        var item = od.Popitem(false);
        item.Should().Be(("a", 1));
        od.Count.Should().Be(1);
    }

    [Fact]
    public void OrderedDict_Popitem_Empty_ThrowsKeyError()
    {
        var od = new Sharpy.OrderedDict<string, int>();
        FluentActions.Invoking(() => od.Popitem())
            .Should().Throw<Sharpy.KeyError>();
    }

    [Fact]
    public void OrderedDict_Remove_MaintainsOrder()
    {
        var od = new Sharpy.OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;
        od.Remove("b");
        od.Keys.ToList().Should().Equal("a", "c");
    }

    // --- ChainMap ---

    [Fact]
    public void ChainMap_LooksUpThroughMaps()
    {
        var d1 = new System.Collections.Generic.Dictionary<string, int> { { "a", 1 } };
        var d2 = new System.Collections.Generic.Dictionary<string, int> { { "b", 2 }, { "a", 99 } };
        var cm = new Sharpy.ChainMap<string, int>(d1, d2);
        cm["a"].Should().Be(1); // First map wins
        cm["b"].Should().Be(2);
    }

    [Fact]
    public void ChainMap_WritesGoToFirstMap()
    {
        var d1 = new System.Collections.Generic.Dictionary<string, int>();
        var d2 = new System.Collections.Generic.Dictionary<string, int> { { "x", 10 } };
        var cm = new Sharpy.ChainMap<string, int>(d1, d2);
        cm["y"] = 20;
        d1.Should().ContainKey("y");
        d2.Should().NotContainKey("y");
    }

    [Fact]
    public void ChainMap_NewChild_PrependsMap()
    {
        var d1 = new System.Collections.Generic.Dictionary<string, int> { { "a", 1 } };
        var cm = new Sharpy.ChainMap<string, int>(d1);
        var child = cm.NewChild(new System.Collections.Generic.Dictionary<string, int> { { "a", 99 } });
        child["a"].Should().Be(99);
        cm["a"].Should().Be(1);
    }

    [Fact]
    public void ChainMap_MissingKey_ThrowsKeyError()
    {
        var cm = new Sharpy.ChainMap<string, int>();
        FluentActions.Invoking(() => { var x = cm["missing"]; })
            .Should().Throw<Sharpy.KeyError>();
    }

    [Fact]
    public void ChainMap_Parents_ExcludesFirst()
    {
        var d1 = new System.Collections.Generic.Dictionary<string, int> { { "a", 1 } };
        var d2 = new System.Collections.Generic.Dictionary<string, int> { { "b", 2 } };
        var cm = new Sharpy.ChainMap<string, int>(d1, d2);
        var parents = cm.Parents;
        parents.ContainsKey("a").Should().BeFalse();
        parents.ContainsKey("b").Should().BeTrue();
    }
}
