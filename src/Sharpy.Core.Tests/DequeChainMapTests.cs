using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional Deque and ChainMap tests not covered by CollectionsModuleTests.cs
/// or CollectionsAdditionalTests.cs.
/// </summary>
public class DequeAdditional_Tests
{
    #region Deque Construction Edge Cases

    [Fact]
    public void Deque_ConstructFromEmpty_IsEmpty()
    {
        var deque = new Sharpy.Deque<int>(new int[0]);

        deque.Count.Should().Be(0);
        FluentActions.Invoking(() => deque.Pop()).Should().Throw<Sharpy.IndexError>();
    }

    [Fact]
    public void Deque_AppendAfterPop_CountReturnsToZero()
    {
        var deque = new Sharpy.Deque<int>();
        deque.Append(42);
        deque.Pop();

        deque.Count.Should().Be(0);
    }

    #endregion

    #region Deque Extend Edge Cases

    [Fact]
    public void Deque_Extend_OnEmpty_AddsAllToRight()
    {
        var deque = new Sharpy.Deque<int>();
        deque.Extend(new[] { 1, 2, 3 });

        deque.Count.Should().Be(3);
        deque.Popleft().Should().Be(1);
        deque.Popleft().Should().Be(2);
        deque.Popleft().Should().Be(3);
    }

    [Fact]
    public void Deque_Extendleft_OnEmpty_ReversesOrder()
    {
        // Python: extendleft([1,2,3]) each goes to left → [3,2,1]
        var deque = new Sharpy.Deque<int>();
        deque.Extendleft(new[] { 1, 2, 3 });

        deque.Count.Should().Be(3);
        deque.Popleft().Should().Be(3);
        deque.Popleft().Should().Be(2);
        deque.Popleft().Should().Be(1);
    }

    [Fact]
    public void Deque_Extend_EmptyIterable_NoChange()
    {
        var deque = new Sharpy.Deque<int>(new[] { 1, 2 });
        deque.Extend(new int[0]);

        deque.Count.Should().Be(2);
    }

    [Fact]
    public void Deque_Extendleft_EmptyIterable_NoChange()
    {
        var deque = new Sharpy.Deque<int>(new[] { 1, 2 });
        deque.Extendleft(new int[0]);

        deque.Count.Should().Be(2);
    }

    #endregion

    #region Deque Re-enumeration

    [Fact]
    public void Deque_IterateTwice_YieldsSameElements()
    {
        var deque = new Sharpy.Deque<int>(new[] { 10, 20, 30 });

        var first = new List<int>();
        foreach (var x in deque)
            first.Add(x);

        var second = new List<int>();
        foreach (var x in deque)
            second.Add(x);

        first.Should().Equal(second);
        first.Should().Equal(10, 20, 30);
    }

    #endregion

    #region Deque Clear and Rebuild

    [Fact]
    public void Deque_ClearAndRebuild_WorksCorrectly()
    {
        var deque = new Sharpy.Deque<string>(new[] { "a", "b", "c" });
        deque.Clear();

        deque.Append("x");
        deque.Append("y");

        deque.Count.Should().Be(2);
        deque.Popleft().Should().Be("x");
        deque.Popleft().Should().Be("y");
    }

    #endregion
}

public class ChainMapAdditional_Tests
{
    #region Contains Alias

    [Fact]
    public void ChainMap_Contains_SearchesAllMaps()
    {
        var d1 = new Dict<string, int>();
        d1["a"] = 1;
        var d2 = new Dict<string, int>();
        d2["b"] = 2;

        var cm = new ChainMap<string, int>(d1, d2);

        cm.Contains("a").Should().BeTrue();
        cm.Contains("b").Should().BeTrue();
        cm.Contains("c").Should().BeFalse();
    }

    #endregion

    #region Pop Only from First Map

    [Fact]
    public void ChainMap_Pop_KeyOnlyInSecondMap_ThrowsKeyError()
    {
        // Python: ChainMap.pop only removes from first map
        var d1 = new Dict<string, int>();
        var d2 = new Dict<string, int>();
        d2["b"] = 2;

        var cm = new ChainMap<string, int>(d1, d2);

        // 'b' exists in the chainmap (via d2) but pop should fail
        // since it's not in d1 (first map)
        FluentActions.Invoking(() => cm.Pop("b"))
            .Should().Throw<Sharpy.KeyError>();
    }

    #endregion

    #region Get Without Default

    [Fact]
    public void ChainMap_Get_MissingKey_NoDefault_ReturnsDefaultT()
    {
        var cm = new ChainMap<string, int>();

        cm.Get("missing").Should().Be(0); // default(int)
    }

    [Fact]
    public void ChainMap_Get_ExistingKey_InSecondMap()
    {
        var d1 = new Dict<string, int>();
        var d2 = new Dict<string, int>();
        d2["x"] = 42;

        var cm = new ChainMap<string, int>(d1, d2);

        cm.Get("x").Should().Be(42);
    }

    #endregion

    #region Write to Empty ChainMap

    [Fact]
    public void ChainMap_Write_GoesToFirstMap_MakesItVisible()
    {
        var cm = new ChainMap<string, int>();
        cm["key"] = 99;

        cm["key"].Should().Be(99);
        cm.Maps[0].ContainsKey("key").Should().BeTrue();
    }

    #endregion

    #region NewChild Write Isolation

    [Fact]
    public void ChainMap_NewChild_WritesToChildMap_NotParent()
    {
        var parent = new Dict<string, int>();
        parent["shared"] = 1;
        var cm = new ChainMap<string, int>(parent);

        var child = cm.NewChild();
        child["new"] = 99;

        // Parent should not see "new"
        parent.ContainsKey("new").Should().BeFalse();
        // Child sees both
        child.ContainsKey("new").Should().BeTrue();
        child["shared"].Should().Be(1);
    }

    #endregion

    #region Maps Property

    [Fact]
    public void ChainMap_Maps_ContainsOriginalReferences()
    {
        var d1 = new Dict<string, int>();
        var d2 = new Dict<string, int>();

        var cm = new ChainMap<string, int>(d1, d2);

        cm.Maps.Should().HaveCount(2);
        cm.Maps[0].Should().BeSameAs(d1);
        cm.Maps[1].Should().BeSameAs(d2);
    }

    [Fact]
    public void ChainMap_Maps_MutatingFirstMap_ReflectedInLookup()
    {
        var d1 = new Dict<string, int>();
        var cm = new ChainMap<string, int>(d1);

        // Directly mutate d1 after creating the ChainMap
        d1["added_later"] = 55;

        cm["added_later"].Should().Be(55);
    }

    #endregion
}
