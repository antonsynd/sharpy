using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ArrayHelpers_Tests
{
    [Fact]
    public void GetItem_PositiveIndex_ReturnsElement()
    {
        var arr = new[] { 10, 20, 30 };

        ArrayHelpers.GetItem(arr, 0).Should().Be(10);
        ArrayHelpers.GetItem(arr, 1).Should().Be(20);
        ArrayHelpers.GetItem(arr, 2).Should().Be(30);
    }

    [Fact]
    public void GetItem_NegativeIndex_ReturnsFromEnd()
    {
        var arr = new[] { 10, 20, 30 };

        ArrayHelpers.GetItem(arr, -1).Should().Be(30);
        ArrayHelpers.GetItem(arr, -2).Should().Be(20);
        ArrayHelpers.GetItem(arr, -3).Should().Be(10);
    }

    [Fact]
    public void GetItem_OutOfBounds_ThrowsIndexError()
    {
        var arr = new[] { 10, 20, 30 };

        var act1 = () => ArrayHelpers.GetItem(arr, 3);
        act1.Should().Throw<IndexError>();

        var act2 = () => ArrayHelpers.GetItem(arr, -4);
        act2.Should().Throw<IndexError>();
    }

    [Fact]
    public void SetItem_PositiveIndex_SetsElement()
    {
        var arr = new[] { 10, 20, 30 };

        ArrayHelpers.SetItem(arr, 1, 99);

        arr[1].Should().Be(99);
    }

    [Fact]
    public void SetItem_NegativeIndex_SetsFromEnd()
    {
        var arr = new[] { 10, 20, 30 };

        ArrayHelpers.SetItem(arr, -1, 99);

        arr[2].Should().Be(99);
    }

    [Fact]
    public void SetItem_OutOfBounds_ThrowsIndexError()
    {
        var arr = new[] { 10, 20, 30 };

        var act1 = () => ArrayHelpers.SetItem(arr, 3, 99);
        act1.Should().Throw<IndexError>();

        var act2 = () => ArrayHelpers.SetItem(arr, -4, 99);
        act2.Should().Throw<IndexError>();
    }
}
