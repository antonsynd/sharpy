using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class MinMaxDefault_Tests
{
    // ── Min with default ──

    [Fact]
    public void Min_EmptyList_WithDefault_ReturnsDefault()
    {
        var empty = new List<int>();
        Min(empty, 0).Should().Be(0);
    }

    [Fact]
    public void Min_NonEmptyList_WithDefault_ReturnsActualMin()
    {
        List<int> list = [5, 3, 7, 1, 9];
        Min(list, 0).Should().Be(1);
    }

    [Fact]
    public void Min_EmptyList_WithNonZeroDefault_ReturnsDefault()
    {
        var empty = new List<int>();
        Min(empty, 99).Should().Be(99);
    }

    [Fact]
    public void Min_EmptyList_WithKeyAndDefault_ReturnsDefault()
    {
        var empty = new List<string>();
        Min(empty, (string s) => s.Length, "fallback").Should().Be("fallback");
    }

    [Fact]
    public void Min_NonEmptyList_WithKeyAndDefault_ReturnsElementWithMinKey()
    {
        List<string> list = ["banana", "fig", "cherry"];
        Min(list, (string s) => s.Length, "fallback").Should().Be("fig");
    }

    [Fact]
    public void Min_SingleElementList_WithDefault_ReturnsThatElement()
    {
        List<int> list = [42];
        Min(list, 0).Should().Be(42);
    }

    [Fact]
    public void Min_NullIterable_WithDefault_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Min<int>(null!, 0))
            .Should().Throw<TypeError>();
    }

    // ── Max with default ──

    [Fact]
    public void Max_EmptyList_WithDefault_ReturnsDefault()
    {
        var empty = new List<int>();
        Max(empty, 42).Should().Be(42);
    }

    [Fact]
    public void Max_NonEmptyList_WithDefault_ReturnsActualMax()
    {
        List<int> list = [5, 3, 7, 1, 9];
        Max(list, 0).Should().Be(9);
    }

    [Fact]
    public void Max_EmptyList_WithKeyAndDefault_ReturnsDefault()
    {
        var empty = new List<string>();
        Max(empty, (string s) => s.Length, "fallback").Should().Be("fallback");
    }

    [Fact]
    public void Max_NonEmptyList_WithKeyAndDefault_ReturnsElementWithMaxKey()
    {
        List<string> list = ["fig", "cherry", "kiwi"];
        Max(list, (string s) => s.Length, "fallback").Should().Be("cherry");
    }

    [Fact]
    public void Max_SingleElementList_WithDefault_ReturnsThatElement()
    {
        List<int> list = [42];
        Max(list, 0).Should().Be(42);
    }

    [Fact]
    public void Max_NullIterable_WithDefault_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Max<int>(null!, 0))
            .Should().Throw<TypeError>();
    }
}
