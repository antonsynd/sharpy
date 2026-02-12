using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class DictViews_Tests
{
    [Fact]
    public void Items_ReturnsAllKeyValuePairs()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;

        // Act
        var items = (DictItemsView<Str, int>)dict.Items();

        // Assert
        items.Count.Should().Be(3);
        items.__Contains__(("a", 1)).Should().BeTrue();
        items.__Contains__(("b", 2)).Should().BeTrue();
        items.__Contains__(("c", 3)).Should().BeTrue();
        items.__Contains__(("d", 4)).Should().BeFalse();
    }

    [Fact]
    public void Items_ReflectsChangesToDict()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        var items = (DictItemsView<Str, int>)dict.Items();

        // Act - modify dict after getting view
        dict["b"] = 2;

        // Assert - view should reflect the change
        items.Count.Should().Be(2);
        items.__Contains__(("b", 2)).Should().BeTrue();
    }

    [Fact]
    public void Items_IteratesCorrectly()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;

        // Act
        var items = dict.Items();
        var itemList = new List<(Str, int)>();
        foreach (var item in items)
        {
            itemList.Add(item);
        }

        // Assert
        itemList.Should().HaveCount(3);
        itemList.Should().Contain(("a", 1));
        itemList.Should().Contain(("b", 2));
        itemList.Should().Contain(("c", 3));
    }

    [Fact]
    public void Items_ContainsChecksValue()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;

        // Act
        var items = (DictItemsView<Str, int>)dict.Items();

        // Assert
        items.__Contains__(("a", 1)).Should().BeTrue();
        items.__Contains__(("a", 2)).Should().BeFalse(); // Wrong value
    }

    [Fact]
    public void Values_ReturnsAllValues()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;

        // Act
        var values = (DictValuesView<Str, int>)dict.Values();

        // Assert
        values.Count.Should().Be(3);
        values.__Contains__(1).Should().BeTrue();
        values.__Contains__(2).Should().BeTrue();
        values.__Contains__(3).Should().BeTrue();
        values.__Contains__(4).Should().BeFalse();
    }

    [Fact]
    public void Values_ReflectsChangesToDict()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        var values = (DictValuesView<Str, int>)dict.Values();

        // Act - modify dict after getting view
        dict["b"] = 2;

        // Assert - view should reflect the change
        values.Count.Should().Be(2);
        values.__Contains__(2).Should().BeTrue();
    }

    [Fact]
    public void Values_IteratesCorrectly()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;

        // Act
        var values = dict.Values();
        var valueList = new List<int>();
        foreach (var value in values)
        {
            valueList.Add(value);
        }

        // Assert
        valueList.Should().HaveCount(3);
        valueList.Should().Contain(1);
        valueList.Should().Contain(2);
        valueList.Should().Contain(3);
    }

    [Fact]
    public void Values_AllowsDuplicates()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        dict["b"] = 1;
        dict["c"] = 2;

        // Act
        var values = (DictValuesView<Str, int>)dict.Values();

        // Assert
        values.Count.Should().Be(3);
        values.__Contains__(1).Should().BeTrue();
    }

    [Fact]
    public void Keys_IteratesCorrectly()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict["c"] = 3;

        // Act
        var keys = dict.Keys();
        var keyList = new List<Str>();
        foreach (var key in keys)
        {
            keyList.Add(key);
        }

        // Assert
        keyList.Should().HaveCount(3);
        keyList.Should().Contain(new Str("a"));
        keyList.Should().Contain(new Str("b"));
        keyList.Should().Contain(new Str("c"));
    }

    [Fact]
    public void Keys_ReflectsChangesToDict()
    {
        // Arrange
        var dict = new Dict<Str, int>();
        dict["a"] = 1;
        var keys = (DictKeyView<Str, int>)dict.Keys();

        // Act - modify dict after getting view
        dict["b"] = 2;

        // Assert - view should reflect the change
        keys.Count.Should().Be(2);
        keys.__Contains__("b").Should().BeTrue();
    }

    [Fact]
    public void EmptyDict_ViewsHaveZeroLength()
    {
        // Arrange
        var dict = new Dict<Str, int>();

        // Act & Assert
        dict.Keys().Count.Should().Be(0);
        dict.Values().Count.Should().Be(0);
        dict.Items().Count.Should().Be(0);
    }
}
