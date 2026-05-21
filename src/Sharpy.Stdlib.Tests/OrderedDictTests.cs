using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional OrderedDict tests not covered by CollectionsAdditionalTests.cs.
/// </summary>
public class OrderedDictAdditional_Tests
{
    #region Contains Alias

    [Fact]
    public void OrderedDict_Contains_ExistingKey_ReturnsTrue()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;

        od.Contains("a").Should().BeTrue();
    }

    [Fact]
    public void OrderedDict_Contains_MissingKey_ReturnsFalse()
    {
        var od = new OrderedDict<string, int>();

        od.Contains("z").Should().BeFalse();
    }

    #endregion

    #region Values Preserves Order

    [Fact]
    public void OrderedDict_Values_PreservesInsertionOrder()
    {
        var od = new OrderedDict<string, int>();
        od["c"] = 3;
        od["a"] = 1;
        od["b"] = 2;

        od.Values().Should().Equal(3, 1, 2);
    }

    [Fact]
    public void OrderedDict_Values_AfterUpdate_OrderByFirstInsertion()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["a"] = 99; // Update, not re-insert

        od.Values().Should().Equal(99, 2);
    }

    #endregion

    #region MoveToEnd Default Behavior

    [Fact]
    public void OrderedDict_MoveToEnd_DefaultIsLast()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;

        od.MoveToEnd("a"); // default last=true

        od.Keys().Should().Equal("b", "c", "a");
    }

    [Fact]
    public void OrderedDict_MoveToEnd_AlreadyLast_NoChange()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;

        od.MoveToEnd("b", last: true);

        od.Keys().Should().Equal("a", "b");
    }

    [Fact]
    public void OrderedDict_MoveToEnd_AlreadyFirst_NoChange()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;

        od.MoveToEnd("a", last: false);

        od.Keys().Should().Equal("a", "b");
    }

    #endregion

    #region Constructor from KeyValuePairs

    [Fact]
    public void OrderedDict_ConstructFromKvps_PreservesOrder()
    {
        var pairs = new[]
        {
            new KeyValuePair<string, int>("x", 10),
            new KeyValuePair<string, int>("y", 20),
            new KeyValuePair<string, int>("z", 30)
        };
        var od = new OrderedDict<string, int>(pairs);

        od.Keys().Should().Equal("x", "y", "z");
        od.Values().Should().Equal(10, 20, 30);
    }

    #endregion

    #region Removal and Re-insertion

    [Fact]
    public void OrderedDict_AfterRemoval_RemainingItemsCorrectlyIndexed()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;

        od.Pop("b");

        od.Keys().Should().Equal("a", "c");
        od["a"].Should().Be(1);
        od["c"].Should().Be(3);
    }

    [Fact]
    public void OrderedDict_AfterRemoval_ReinsertGoesToEnd()
    {
        var od = new OrderedDict<string, int>();
        od["a"] = 1;
        od["b"] = 2;
        od["c"] = 3;

        od.Pop("a");
        od["a"] = 99; // Re-insert — goes to end

        od.Keys().Should().Equal("b", "c", "a");
        od["a"].Should().Be(99);
    }

    #endregion

    #region Items Preserves Order

    [Fact]
    public void OrderedDict_Items_PreservesInsertionOrder()
    {
        var od = new OrderedDict<string, int>();
        od["z"] = 26;
        od["a"] = 1;
        od["m"] = 13;

        od.Items().Should().Equal(("z", 26), ("a", 1), ("m", 13));
    }

    #endregion

    #region Empty OrderedDict

    [Fact]
    public void OrderedDict_Empty_CountIsZero()
    {
        var od = new OrderedDict<string, int>();

        od.Count.Should().Be(0);
        od.Keys().Should().BeEmpty();
        od.Values().Should().BeEmpty();
        od.Items().Should().BeEmpty();
    }

    [Fact]
    public void OrderedDict_Empty_ContainsReturnsFalse()
    {
        var od = new OrderedDict<string, int>();

        od.ContainsKey("anything").Should().BeFalse();
        od.Contains("anything").Should().BeFalse();
    }

    #endregion
}
