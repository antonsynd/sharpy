using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class DictViewsAdditionalTests
{
    // ===== Items view: insertion-order and after-Clear behavior =====

    [Fact]
    public void Items_IterationOrder_MatchesInsertionOrder()
    {
        var dict = new Dict<string, int>();
        dict["first"] = 1;
        dict["second"] = 2;
        dict["third"] = 3;

        var items = dict.Items();
        var ordered = new System.Collections.Generic.List<(string, int)>();
        foreach (var item in items)
        {
            ordered.Add(item);
        }

        ordered[0].Should().Be(("first", 1));
        ordered[1].Should().Be(("second", 2));
        ordered[2].Should().Be(("third", 3));
    }

    [Fact]
    public void Items_AfterClear_CountIsZero()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        var items = (DictItemsView<string, int>)dict.Items();

        dict.Clear();

        items.Count.Should().Be(0);
    }

    [Fact]
    public void Items_AfterClear_ContainsReturnsFalse()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        var items = (DictItemsView<string, int>)dict.Items();

        dict.Clear();

        items.Contains(("a", 1)).Should().BeFalse();
    }

    // ===== Keys view: insertion-order and after-Clear behavior =====

    [Fact]
    public void Keys_IterationOrder_MatchesInsertionOrder()
    {
        var dict = new Dict<string, int>();
        dict["first"] = 1;
        dict["second"] = 2;
        dict["third"] = 3;

        var keys = dict.Keys();
        var ordered = new System.Collections.Generic.List<string>();
        foreach (var key in keys)
        {
            ordered.Add(key);
        }

        ordered[0].Should().Be("first");
        ordered[1].Should().Be("second");
        ordered[2].Should().Be("third");
    }

    [Fact]
    public void Keys_AfterClear_CountIsZero()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        var keys = (DictKeyView<string, int>)dict.Keys();

        dict.Clear();

        keys.Count.Should().Be(0);
    }

    [Fact]
    public void Keys_Contains_ExistingKey_ReturnsTrue()
    {
        var dict = new Dict<string, int>();
        dict["hello"] = 1;
        var keys = (DictKeyView<string, int>)dict.Keys();

        keys.Contains("hello").Should().BeTrue();
    }

    [Fact]
    public void Keys_Contains_AfterRemoval_ReturnsFalse()
    {
        var dict = new Dict<string, int>();
        dict["hello"] = 1;
        var keys = (DictKeyView<string, int>)dict.Keys();

        dict.Pop("hello");

        keys.Contains("hello").Should().BeFalse();
    }

    // ===== Values view: insertion-order and after-Clear behavior =====

    [Fact]
    public void Values_IterationOrder_MatchesInsertionOrder()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 100;
        dict["b"] = 200;
        dict["c"] = 300;

        var values = dict.Values();
        var ordered = new System.Collections.Generic.List<int>();
        foreach (var v in values)
        {
            ordered.Add(v);
        }

        ordered[0].Should().Be(100);
        ordered[1].Should().Be(200);
        ordered[2].Should().Be(300);
    }

    [Fact]
    public void Values_AfterClear_CountIsZero()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        var values = (DictValuesView<string, int>)dict.Values();

        dict.Clear();

        values.Count.Should().Be(0);
    }

    [Fact]
    public void Values_Contains_AfterValueUpdate_ReflectsNewValue()
    {
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        var values = (DictValuesView<string, int>)dict.Values();

        dict["a"] = 99;

        values.Contains(99).Should().BeTrue();
        values.Contains(1).Should().BeFalse();
    }

    // ===== Cross-view correctness: all three views on same dict =====

    [Fact]
    public void AllViews_EmptyDict_CountsAreZero()
    {
        var dict = new Dict<string, int>();

        dict.Keys().Count.Should().Be(0);
        dict.Values().Count.Should().Be(0);
        dict.Items().Count.Should().Be(0);
    }

    [Fact]
    public void AllViews_AfterSameUpdate_HaveConsistentCounts()
    {
        var dict = new Dict<string, int>();
        var keys = dict.Keys();
        var values = dict.Values();
        var items = dict.Items();

        dict["a"] = 1;
        dict["b"] = 2;

        keys.Count.Should().Be(2);
        values.Count.Should().Be(2);
        items.Count.Should().Be(2);
    }
}
