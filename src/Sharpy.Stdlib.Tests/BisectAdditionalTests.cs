using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class BisectAdditionalTests
{
    // Single-element list: value < element

    [Fact]
    public void BisectLeft_SingleElement_ValueLessThan_ReturnsZero()
    {
        var a = new Sharpy.List<int> { 5 };
        Sharpy.BisectModule.BisectLeft(a, 3).Should().Be(0);
    }

    [Fact]
    public void BisectRight_SingleElement_ValueLessThan_ReturnsZero()
    {
        var a = new Sharpy.List<int> { 5 };
        Sharpy.BisectModule.BisectRight(a, 3).Should().Be(0);
    }

    // Single-element list: value > element

    [Fact]
    public void BisectLeft_SingleElement_ValueGreaterThan_ReturnsOne()
    {
        var a = new Sharpy.List<int> { 5 };
        Sharpy.BisectModule.BisectLeft(a, 7).Should().Be(1);
    }

    [Fact]
    public void BisectRight_SingleElement_ValueGreaterThan_ReturnsOne()
    {
        var a = new Sharpy.List<int> { 5 };
        Sharpy.BisectModule.BisectRight(a, 7).Should().Be(1);
    }

    // All elements equal: BisectLeft returns 0, BisectRight returns len

    [Fact]
    public void BisectLeft_AllElementsEqual_ReturnsZero()
    {
        var a = new Sharpy.List<int> { 3, 3, 3, 3, 3 };
        Sharpy.BisectModule.BisectLeft(a, 3).Should().Be(0);
    }

    [Fact]
    public void BisectRight_AllElementsEqual_ReturnsLength()
    {
        var a = new Sharpy.List<int> { 3, 3, 3, 3, 3 };
        Sharpy.BisectModule.BisectRight(a, 3).Should().Be(5);
    }

    // BisectRight: value smaller/larger than all

    [Fact]
    public void BisectRight_ValueSmallerThanAll_ReturnsZero()
    {
        var a = new Sharpy.List<int> { 10, 20, 30 };
        Sharpy.BisectModule.BisectRight(a, 5).Should().Be(0);
    }

    [Fact]
    public void BisectRight_ValueLargerThanAll_ReturnsLength()
    {
        var a = new Sharpy.List<int> { 10, 20, 30 };
        Sharpy.BisectModule.BisectRight(a, 35).Should().Be(3);
    }

    // BisectLeft with hi bound

    [Fact]
    public void BisectLeft_WithHiBounds_ExcludesElementsBeyondHi()
    {
        // Searching [1,2,3,4,5] up to index 3 (exclusive) — treats list as [1,2,3]
        var a = new Sharpy.List<int> { 1, 2, 3, 4, 5 };
        // Value 4 exists at index 3 but is excluded; result should be 3 (end of search window)
        Sharpy.BisectModule.BisectLeft(a, 4, hi: 3).Should().Be(3);
    }

    // BisectRight with lo bound

    [Fact]
    public void BisectRight_WithLoBounds_ExcludesElementsBeforeLo()
    {
        // Searching [1,2,3,4,5] starting at index 2 — treats list as [3,4,5]
        // Value 3 rightmost insertion in [3,4,5] starting from index 2 is index 3
        var a = new Sharpy.List<int> { 1, 2, 3, 4, 5 };
        Sharpy.BisectModule.BisectRight(a, 3, lo: 2).Should().Be(3);
    }

    // Combined lo + hi boundaries

    [Fact]
    public void BisectLeft_CombinedLoHi_SearchesSubrange()
    {
        // Search [1,2,3,4,5] within [index 1, index 4) = [2,3,4]
        // Value 3 leftmost in that window is at index 2
        var a = new Sharpy.List<int> { 1, 2, 3, 4, 5 };
        Sharpy.BisectModule.BisectLeft(a, 3, lo: 1, hi: 4).Should().Be(2);
    }

    [Fact]
    public void BisectRight_CombinedLoHi_SearchesSubrange()
    {
        // Search [1,2,3,4,5] within [index 1, index 4) = [2,3,4]
        // Value 3 rightmost in that window is at index 3
        var a = new Sharpy.List<int> { 1, 2, 3, 4, 5 };
        Sharpy.BisectModule.BisectRight(a, 3, lo: 1, hi: 4).Should().Be(3);
    }

    // InsortLeft into empty list

    [Fact]
    public void InsortLeft_IntoEmptyList_InsertsSingleElement()
    {
        var a = new Sharpy.List<int>();
        Sharpy.BisectModule.InsortLeft(a, 5);
        a.Should().Equal(5);
    }

    // InsortRight preserves sort order after multiple insertions

    [Fact]
    public void InsortRight_MultipleInsertions_MaintainsSortedOrder()
    {
        var a = new Sharpy.List<int>();
        Sharpy.BisectModule.InsortRight(a, 3);
        Sharpy.BisectModule.InsortRight(a, 1);
        Sharpy.BisectModule.InsortRight(a, 5);
        Sharpy.BisectModule.InsortRight(a, 2);
        a.Should().Equal(1, 2, 3, 5);
    }

    // InsortRight with lo bound excludes part of the list from the search

    [Fact]
    public void InsortRight_WithLoBounds_InsertsAfterLo()
    {
        // List: [1,2,5]; insert 4 starting search at index 2 — finds slot at index 2
        var a = new Sharpy.List<int> { 1, 2, 5 };
        Sharpy.BisectModule.InsortRight(a, 4, lo: 2);
        a.Should().Equal(1, 2, 4, 5);
    }
}
