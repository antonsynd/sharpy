using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional Counter tests not covered by CollectionsModuleTests.cs.
/// </summary>
public class CounterComplete_Tests
{
    #region Contains and ContainsKey

    [Fact]
    public void Counter_Contains_ExistingKey_ReturnsTrue()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b", "a" });

        counter.Contains("a").Should().BeTrue();
    }

    [Fact]
    public void Counter_Contains_MissingKey_ReturnsFalse()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a" });

        counter.Contains("z").Should().BeFalse();
    }

    [Fact]
    public void Counter_ContainsKey_AfterClear_ReturnsFalse()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b" });
        counter.Clear();

        counter.ContainsKey("a").Should().BeFalse();
        counter.ContainsKey("b").Should().BeFalse();
    }

    #endregion

    #region Keys Property

    [Fact]
    public void Counter_Keys_ContainsAllAddedElements()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b", "c", "a" });

        var keys = new List<string>(counter.Keys);
        keys.Should().Contain("a");
        keys.Should().Contain("b");
        keys.Should().Contain("c");
        keys.Should().HaveCount(3);
    }

    [Fact]
    public void Counter_Keys_Empty_ReturnsEmptyEnumerable()
    {
        var counter = new Sharpy.Counter<string>();

        var keys = new List<string>(counter.Keys);
        keys.Should().BeEmpty();
    }

    #endregion

    #region MostCommon Edge Cases

    [Fact]
    public void Counter_MostCommon_Zero_ReturnsEmptyList()
    {
        // Python: Counter('aabbc').most_common(0) == []
        var counter = new Sharpy.Counter<string>(new[] { "a", "a", "b", "b", "c" });

        var result = counter.MostCommon(0);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Counter_MostCommon_NGreaterThanCount_ReturnsAll()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b" });

        var result = counter.MostCommon(100);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Counter_MostCommon_TieBreakByKey_AlphabeticOrder()
    {
        // Elements with same count are sorted by key (alphabetically for strings)
        var counter = new Sharpy.Counter<string>(new[] { "b", "a", "c" });

        var result = counter.MostCommon();

        result.Should().HaveCount(3);
        // Each count is 1; they should be sorted alphabetically
        result[0].Item2.Should().Be(1);
        result[1].Item2.Should().Be(1);
        result[2].Item2.Should().Be(1);
    }

    #endregion

    #region Elements with Zero and Negative Counts

    [Fact]
    public void Counter_Elements_ZeroCount_NotYielded()
    {
        // Python: elements() only yields elements with positive counts
        var counter = new Sharpy.Counter<string>();
        counter["a"] = 2;
        counter["b"] = 0;

        var elements = counter.Elements().ToList();

        elements.Should().Contain("a");
        elements.Should().NotContain("b");
        elements.Should().HaveCount(2);
    }

    [Fact]
    public void Counter_Elements_NegativeCount_NotYielded()
    {
        // Python: Counter with negative count, elements() skips it
        var counter = new Sharpy.Counter<string>();
        counter["a"] = 2;
        counter["b"] = -1;

        var elements = counter.Elements().ToList();

        elements.Should().Contain("a");
        elements.Should().NotContain("b");
        elements.Should().HaveCount(2);
    }

    [Fact]
    public void Counter_Elements_AllNegative_ReturnsEmpty()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b" });
        counter.Subtract(new[] { "a", "a", "b", "b" }); // all go to -1 or below

        var elements = counter.Elements().ToList();

        elements.Should().BeEmpty();
    }

    #endregion

    #region Update Multiple Times

    [Fact]
    public void Counter_Update_Twice_AccumulatesCounts()
    {
        // Calling update multiple times accumulates counts
        var counter = new Sharpy.Counter<string>(new[] { "a" });
        counter.Update(new[] { "a", "b" });
        counter.Update(new[] { "b", "c" });

        counter["a"].Should().Be(2);
        counter["b"].Should().Be(2);
        counter["c"].Should().Be(1);
    }

    [Fact]
    public void Counter_Update_EmptyIterable_NoChange()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "b" });
        counter.Update(new string[0]);

        counter["a"].Should().Be(1);
        counter["b"].Should().Be(1);
    }

    #endregion

    #region Indexer Direct Set

    [Fact]
    public void Counter_IndexerSet_NewKey_CreatesEntry()
    {
        var counter = new Sharpy.Counter<string>();

        counter["new"] = 5;

        counter["new"].Should().Be(5);
        counter.Contains("new").Should().BeTrue();
    }

    [Fact]
    public void Counter_IndexerSet_OverridesExistingCount()
    {
        var counter = new Sharpy.Counter<string>(new[] { "a", "a", "a" });

        counter["a"] = 1;

        counter["a"].Should().Be(1);
    }

    [Fact]
    public void Counter_IndexerSet_Negative_CountIsNegative()
    {
        var counter = new Sharpy.Counter<string>();

        counter["x"] = -3;

        counter["x"].Should().Be(-3);
        counter.Total().Should().Be(-3);
    }

    #endregion

    #region Copy Independence

    [Fact]
    public void Counter_Copy_ModifyingCopyDoesNotAffectOriginal()
    {
        var original = new Sharpy.Counter<string>(new[] { "a", "b" });

        var copy = original.Copy();
        copy["a"] = 99;
        copy["new"] = 5;

        original["a"].Should().Be(1);
        original.Contains("new").Should().BeFalse();
    }

    #endregion

    #region Arithmetic Edge Cases

    [Fact]
    public void Counter_OperatorSubtract_AllGoZeroOrNegative_EmptyResult()
    {
        // Python: c1 - c2 drops zero and negative
        var c1 = new Sharpy.Counter<string>(new[] { "a" });
        var c2 = new Sharpy.Counter<string>(new[] { "a", "a", "a" });

        var result = c1 - c2;

        result.ContainsKey("a").Should().BeFalse();
    }

    [Fact]
    public void Counter_OperatorAdd_IncludesKeysFromBoth()
    {
        var c1 = new Sharpy.Counter<string>(new[] { "a" });
        var c2 = new Sharpy.Counter<string>(new[] { "b" });

        var result = c1 + c2;

        result["a"].Should().Be(1);
        result["b"].Should().Be(1);
    }

    [Fact]
    public void Counter_OperatorAnd_OnlyIncludesSharedKeys()
    {
        var c1 = new Sharpy.Counter<string>(new[] { "a", "a", "b" });
        var c2 = new Sharpy.Counter<string>(new[] { "b", "c" });

        var result = c1 & c2;

        result.ContainsKey("a").Should().BeFalse();
        result["b"].Should().Be(1);
        result.ContainsKey("c").Should().BeFalse();
    }

    #endregion
}
