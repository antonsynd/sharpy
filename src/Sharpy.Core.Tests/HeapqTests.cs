using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Heapq_Tests
{
    [Fact]
    public void Heappush_MaintainsMinHeap()
    {
        var h = new List<int>();
        Sharpy.Heapq.Heappush(h, 3);
        Sharpy.Heapq.Heappush(h, 1);
        Sharpy.Heapq.Heappush(h, 2);

        h[0].Should().Be(1);
    }

    [Fact]
    public void Heappop_ReturnsSmallest()
    {
        var h = new List<int>();
        Sharpy.Heapq.Heappush(h, 3);
        Sharpy.Heapq.Heappush(h, 1);
        Sharpy.Heapq.Heappush(h, 2);

        Sharpy.Heapq.Heappop(h).Should().Be(1);
        Sharpy.Heapq.Heappop(h).Should().Be(2);
        Sharpy.Heapq.Heappop(h).Should().Be(3);
    }

    [Fact]
    public void Heappop_EmptyHeap_ThrowsIndexError()
    {
        var h = new List<int>();
        FluentActions.Invoking(() => Sharpy.Heapq.Heappop(h))
            .Should().Throw<Sharpy.IndexError>()
            .WithMessage("index out of range");
    }

    [Fact]
    public void Heapify_CreatesValidMinHeap()
    {
        var h = new List<int> { 5, 3, 1, 4, 2 };
        Sharpy.Heapq.Heapify(h);

        // The smallest element must be at index 0
        h[0].Should().Be(1);

        // Verify heap property: parent <= children
        for (int i = 0; i < ((ICollection<int>)h).Count; i++)
        {
            int left = 2 * i + 1;
            int right = 2 * i + 2;
            if (left < ((ICollection<int>)h).Count)
            {
                h[i].Should().BeLessThanOrEqualTo(h[left]);
            }
            if (right < ((ICollection<int>)h).Count)
            {
                h[i].Should().BeLessThanOrEqualTo(h[right]);
            }
        }
    }

    [Fact]
    public void Heapify_MatchesPythonOutput()
    {
        var h = new List<int> { 5, 3, 1, 4, 2 };
        Sharpy.Heapq.Heapify(h);

        // Python produces [1, 2, 5, 4, 3]
        h.Should().Equal(1, 2, 5, 4, 3);
    }

    [Fact]
    public void Heapreplace_PopsAndPushes()
    {
        var h = new List<int> { 1, 2, 3 };
        var result = Sharpy.Heapq.Heapreplace(h, 0);

        // Returns the old smallest (1), pushes 0
        result.Should().Be(1);
        h[0].Should().Be(0);
    }

    [Fact]
    public void Heapreplace_EmptyHeap_ThrowsIndexError()
    {
        var h = new List<int>();
        FluentActions.Invoking(() => Sharpy.Heapq.Heapreplace(h, 1))
            .Should().Throw<Sharpy.IndexError>();
    }

    [Fact]
    public void Heappushpop_PushThenPop()
    {
        var h = new List<int> { 1, 2, 3 };

        // Pushing 0 then popping should return 0 (the new smallest)
        var result = Sharpy.Heapq.Heappushpop(h, 0);
        result.Should().Be(0);
        ((ICollection<int>)h).Count.Should().Be(3);
    }

    [Fact]
    public void Heappushpop_ItemLargerThanSmallest()
    {
        var h = new List<int> { 1, 2, 3 };

        // Pushing 4 then popping should return 1 (the old smallest)
        var result = Sharpy.Heapq.Heappushpop(h, 4);
        result.Should().Be(1);
        h[0].Should().BeLessThanOrEqualTo(((ICollection<int>)h).Count > 1 ? h[1] : int.MaxValue);
    }

    [Fact]
    public void Nlargest_ReturnsNLargestDescending()
    {
        IList<int> data = new List<int> { 3, 1, 4, 1, 5, 9, 2, 6 };
        var result = Sharpy.Heapq.Nlargest(3, data);

        var items = new List<int>();
        foreach (int item in (IEnumerable<int>)result)
        {
            items.Add(item);
        }
        items.Should().Equal(9, 6, 5);
    }

    [Fact]
    public void Nsmallest_ReturnsNSmallestAscending()
    {
        IList<int> data = new List<int> { 3, 1, 4, 1, 5, 9, 2, 6 };
        var result = Sharpy.Heapq.Nsmallest(3, data);

        var items = new List<int>();
        foreach (int item in (IEnumerable<int>)result)
        {
            items.Add(item);
        }
        items.Should().Equal(1, 1, 2);
    }

    [Fact]
    public void Nlargest_ZeroN_ReturnsEmpty()
    {
        IList<int> data = new List<int> { 1, 2, 3 };
        var result = Sharpy.Heapq.Nlargest(0, data);

        ((ICollection<int>)result).Count.Should().Be(0);
    }

    [Fact]
    public void Nsmallest_NLargerThanList_ReturnsAll()
    {
        IList<int> data = new List<int> { 3, 1, 2 };
        var result = Sharpy.Heapq.Nsmallest(10, data);

        var items = new List<int>();
        foreach (int item in (IEnumerable<int>)result)
        {
            items.Add(item);
        }
        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Heappush_SingleElement()
    {
        var h = new List<int>();
        Sharpy.Heapq.Heappush(h, 42);
        h.Should().Equal(42);
    }

    [Fact]
    public void Heappop_SingleElement()
    {
        var h = new List<int> { 42 };
        Sharpy.Heapq.Heappop(h).Should().Be(42);
        h.Should().BeEmpty();
    }

    [Fact]
    public void Heapify_EmptyList_NoOp()
    {
        var h = new List<int>();
        Sharpy.Heapq.Heapify(h);
        h.Should().BeEmpty();
    }

    [Fact]
    public void Heapify_AlreadySorted()
    {
        var h = new List<int> { 1, 2, 3, 4, 5 };
        Sharpy.Heapq.Heapify(h);
        h[0].Should().Be(1);
    }

    [Fact]
    public void PushPop_Sequence_ProducesSortedOutput()
    {
        var h = new List<int>();
        var input = new[] { 5, 3, 8, 1, 9, 2, 7, 4, 6 };
        foreach (var item in input)
        {
            Sharpy.Heapq.Heappush(h, item);
        }

        var sorted = new List<int>();
        while (((ICollection<int>)h).Count > 0)
        {
            sorted.Add(Sharpy.Heapq.Heappop(h));
        }

        sorted.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9);
    }

    [Fact]
    public void Merge_TwoSortedLists()
    {
        var a = new List<int> { 1, 3, 5 };
        var b = new List<int> { 2, 4, 6 };
        var result = new System.Collections.Generic.List<int>();
        foreach (var item in Sharpy.Heapq.Merge(a, b))
        {
            result.Add(item);
        }

        result.Should().Equal(1, 2, 3, 4, 5, 6);
    }

    [Fact]
    public void Merge_ThreeSortedLists()
    {
        var a = new List<int> { 1, 4 };
        var b = new List<int> { 2, 5 };
        var c = new List<int> { 3, 6 };
        var result = new System.Collections.Generic.List<int>();
        foreach (var item in Sharpy.Heapq.Merge(a, b, c))
        {
            result.Add(item);
        }

        result.Should().Equal(1, 2, 3, 4, 5, 6);
    }

    [Fact]
    public void Merge_EmptyIterables()
    {
        var a = new List<int>();
        var b = new List<int> { 1, 2 };
        var c = new List<int>();
        var result = new System.Collections.Generic.List<int>();
        foreach (var item in Sharpy.Heapq.Merge(a, b, c))
        {
            result.Add(item);
        }

        result.Should().Equal(1, 2);
    }

    [Fact]
    public void Merge_AllEmpty()
    {
        var a = new List<int>();
        var b = new List<int>();
        var result = new System.Collections.Generic.List<int>();
        foreach (var item in Sharpy.Heapq.Merge(a, b))
        {
            result.Add(item);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Merge_SingleIterable()
    {
        var a = new List<int> { 1, 2, 3 };
        var result = new System.Collections.Generic.List<int>();
        foreach (var item in Sharpy.Heapq.Merge(a))
        {
            result.Add(item);
        }

        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Merge_NoIterables()
    {
        var result = new System.Collections.Generic.List<int>();
        foreach (var item in Sharpy.Heapq.Merge<int>())
        {
            result.Add(item);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Merge_EarlyBreak_DisposesEnumerators()
    {
        var a = new List<int> { 1, 3, 5, 7, 9 };
        var b = new List<int> { 2, 4, 6, 8, 10 };
        var result = new System.Collections.Generic.List<int>();

        // Take only first 3 elements — should not leak enumerators
        int count = 0;
        foreach (var item in Sharpy.Heapq.Merge(a, b))
        {
            result.Add(item);
            count++;
            if (count >= 3)
            {
                break;
            }
        }

        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Merge_WithReverse_YieldsDescendingOrder()
    {
        // Inputs pre-sorted descending
        var a = new List<int> { 6, 4, 2 };
        var b = new List<int> { 5, 3, 1 };
        var result = new System.Collections.Generic.List<int>();
        foreach (var item in Sharpy.Heapq.Merge(a, b, reverse: true))
        {
            result.Add(item);
        }

        result.Should().Equal(6, 5, 4, 3, 2, 1);
    }

    [Fact]
    public void Merge_WithKeyFunction_SortsByKey()
    {
        // Inputs pre-sorted ascending by string length
        var a = new List<string> { "fig", "apple", "banana" };
        var b = new List<string> { "hi", "cherry", "elephant" };
        var result = new System.Collections.Generic.List<string>();
        foreach (var item in Sharpy.Heapq.Merge<string, int>(a, b, s => s.Length))
        {
            result.Add(item);
        }

        result.Should().Equal("hi", "fig", "apple", "banana", "cherry", "elephant");
    }

    [Fact]
    public void Merge_WithKeyAndReverse_SortsByKeyDescending()
    {
        // Inputs pre-sorted descending by string length
        var a = new List<string> { "banana", "apple", "fig" };
        var b = new List<string> { "elephant", "cherry", "hi" };
        var result = new System.Collections.Generic.List<string>();
        foreach (var item in Sharpy.Heapq.Merge<string, int>(a, b, s => s.Length, reverse: true))
        {
            result.Add(item);
        }

        result.Should().Equal("elephant", "banana", "cherry", "apple", "fig", "hi");
    }

    [Fact]
    public void Merge_WithReverse_ThreeLists()
    {
        // Inputs pre-sorted descending
        var a = new List<int> { 9, 6, 3 };
        var b = new List<int> { 8, 5, 2 };
        var c = new List<int> { 7, 4, 1 };
        var result = new System.Collections.Generic.List<int>();
        foreach (var item in Sharpy.Heapq.Merge(a, b, c, reverse: true))
        {
            result.Add(item);
        }

        result.Should().Equal(9, 8, 7, 6, 5, 4, 3, 2, 1);
    }

    [Fact]
    public void Merge_WithKey_EmptyLists()
    {
        var a = new List<string>();
        var b = new List<string>();
        var result = new System.Collections.Generic.List<string>();
        foreach (var item in Sharpy.Heapq.Merge<string, int>(a, b, s => s.Length))
        {
            result.Add(item);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Merge_WithReverse_SingleList()
    {
        var a = new List<int> { 3, 2, 1 };
        var b = new List<int>();
        var result = new System.Collections.Generic.List<int>();
        foreach (var item in Sharpy.Heapq.Merge(a, b, reverse: true))
        {
            result.Add(item);
        }

        result.Should().Equal(3, 2, 1);
    }
}
