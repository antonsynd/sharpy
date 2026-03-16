using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Bisect_Tests
{
    [Fact]
    public void BisectLeft_FindsLeftmostInsertionPoint()
    {
        var a = new List<int> { 1, 2, 3, 4, 5 };
        Sharpy.Bisect.BisectLeft(a, 3).Should().Be(2);
    }

    [Fact]
    public void BisectRight_FindsRightmostInsertionPoint()
    {
        var a = new List<int> { 1, 2, 3, 4, 5 };
        Sharpy.Bisect.BisectRight(a, 3).Should().Be(3);
    }

    [Fact]
    public void BisectLeft_WithDuplicates_ReturnsLeftmost()
    {
        var a = new List<int> { 1, 1, 1 };
        Sharpy.Bisect.BisectLeft(a, 1).Should().Be(0);
    }

    [Fact]
    public void BisectRight_WithDuplicates_ReturnsRightmost()
    {
        var a = new List<int> { 1, 1, 1 };
        Sharpy.Bisect.BisectRight(a, 1).Should().Be(3);
    }

    [Fact]
    public void BisectLeft_EmptyList_ReturnsZero()
    {
        var a = new List<int>();
        Sharpy.Bisect.BisectLeft(a, 1).Should().Be(0);
    }

    [Fact]
    public void BisectRight_EmptyList_ReturnsZero()
    {
        var a = new List<int>();
        Sharpy.Bisect.BisectRight(a, 1).Should().Be(0);
    }

    [Fact]
    public void BisectLeft_ValueSmallerThanAll_ReturnsZero()
    {
        var a = new List<int> { 10, 20, 30 };
        Sharpy.Bisect.BisectLeft(a, 5).Should().Be(0);
    }

    [Fact]
    public void BisectLeft_ValueLargerThanAll_ReturnsLength()
    {
        var a = new List<int> { 10, 20, 30 };
        Sharpy.Bisect.BisectLeft(a, 35).Should().Be(3);
    }

    [Fact]
    public void BisectSearch_IsAliasForBisectRight()
    {
        var a = new List<int> { 1, 2, 3, 4, 5 };
        Sharpy.Bisect.BisectSearch(a, 3).Should().Be(Sharpy.Bisect.BisectRight(a, 3));
    }

    [Fact]
    public void BisectLeft_WithLoBounds()
    {
        var a = new List<int> { 1, 2, 3, 4, 5 };
        Sharpy.Bisect.BisectLeft(a, 3, lo: 3).Should().Be(3);
    }

    [Fact]
    public void BisectRight_WithHiBounds()
    {
        var a = new List<int> { 1, 2, 3, 4, 5 };
        Sharpy.Bisect.BisectRight(a, 3, hi: 2).Should().Be(2);
    }

    [Fact]
    public void InsortRight_InsertsInSortedOrder()
    {
        var a = new List<int> { 1, 3, 5 };
        Sharpy.Bisect.InsortRight(a, 4);
        a.Should().Equal(1, 3, 4, 5);
    }

    [Fact]
    public void InsortLeft_InsertsAtLeftPosition()
    {
        var a = new List<int> { 1, 3, 3, 5 };
        Sharpy.Bisect.InsortLeft(a, 3);
        a.Should().Equal(1, 3, 3, 3, 5);
        // The new 3 should be at index 1 (leftmost)
    }

    [Fact]
    public void Insort_IsAliasForInsortRight()
    {
        var a1 = new List<int> { 1, 3, 5 };
        var a2 = new List<int> { 1, 3, 5 };
        Sharpy.Bisect.Insort(a1, 4);
        Sharpy.Bisect.InsortRight(a2, 4);
        a1.Should().Equal(a2);
    }

    [Fact]
    public void Insort_IntoEmptyList()
    {
        var a = new List<int>();
        Sharpy.Bisect.Insort(a, 5);
        a.Should().Equal(5);
    }

    [Fact]
    public void InsortLeft_WithLoBounds()
    {
        var a = new List<int> { 1, 2, 3, 5 };
        Sharpy.Bisect.InsortLeft(a, 4, lo: 2);
        a.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void BisectLeft_SingleElement_ValueEqual()
    {
        var a = new List<int> { 5 };
        Sharpy.Bisect.BisectLeft(a, 5).Should().Be(0);
    }

    [Fact]
    public void BisectRight_SingleElement_ValueEqual()
    {
        var a = new List<int> { 5 };
        Sharpy.Bisect.BisectRight(a, 5).Should().Be(1);
    }

    [Fact]
    public void BisectLeft_WithStrings()
    {
        var a = new List<string> { "apple", "banana", "cherry" };
        Sharpy.Bisect.BisectLeft(a, "banana").Should().Be(1);
    }
}
