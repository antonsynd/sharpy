using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class HeapqAdditionalTests
{
    // Heapreplace on single element: replaces the root and returns old value

    [Fact]
    public void Heapreplace_SingleElement_ReturnsOldValueAndReplaces()
    {
        var h = new List<int> { 42 };
        var result = Sharpy.Heapq.Heapreplace(h, 7);
        result.Should().Be(42);
        h[0].Should().Be(7);
        ((ICollection<int>)h).Count.Should().Be(1);
    }

    // Heappushpop on empty heap: returns the item (no swap since heap is empty)

    [Fact]
    public void Heappushpop_EmptyHeap_ReturnsItemDirectly()
    {
        var h = new List<int>();
        // When heap is empty, heap[0] doesn't exist so item < nothing — returns item
        var result = Sharpy.Heapq.Heappushpop(h, 99);
        result.Should().Be(99);
        ((ICollection<int>)h).Count.Should().Be(0);
    }

    // Heappushpop where pushed value == smallest (edge: equal values)

    [Fact]
    public void Heappushpop_ValueEqualToSmallest_ReturnsItem()
    {
        var h = new List<int> { 1, 2, 3 };
        // item (1) is not less than heap[0] (1), so item is returned, heap unchanged
        var result = Sharpy.Heapq.Heappushpop(h, 1);
        result.Should().Be(1);
        ((ICollection<int>)h).Count.Should().Be(3);
    }

    // Heapify on single element: still a valid heap

    [Fact]
    public void Heapify_SingleElement_RemainsUnchanged()
    {
        var h = new List<int> { 42 };
        Sharpy.Heapq.Heapify(h);
        h.Should().Equal(42);
    }

    // Heapify on already-valid heap: maintains heap property

    [Fact]
    public void Heapify_AlreadyValidHeap_PreservesHeapProperty()
    {
        // [1, 2, 3] is already a valid min-heap
        var h = new List<int> { 1, 2, 3 };
        Sharpy.Heapq.Heapify(h);
        h[0].Should().Be(1);
        // Verify full heap property
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

    // Nsmallest with n == 0: returns empty

    [Fact]
    public void Nsmallest_ZeroN_ReturnsEmpty()
    {
        IList<int> data = new List<int> { 1, 2, 3 };
        var result = Sharpy.Heapq.Nsmallest(0, data);
        ((ICollection<int>)result).Count.Should().Be(0);
    }

    // Nlargest with n > len(iterable): returns all in descending order

    [Fact]
    public void Nlargest_NGreaterThanList_ReturnsAllDescending()
    {
        IList<int> data = new List<int> { 3, 1, 2 };
        var result = Sharpy.Heapq.Nlargest(10, data);
        var items = new List<int>();
        foreach (int item in (IEnumerable<int>)result)
        {
            items.Add(item);
        }
        items.Should().Equal(3, 2, 1);
    }

    // Nlargest with n == len(iterable): returns all in descending order

    [Fact]
    public void Nlargest_NEqualsListLength_ReturnsAllDescending()
    {
        IList<int> data = new List<int> { 5, 3, 8 };
        var result = Sharpy.Heapq.Nlargest(3, data);
        var items = new List<int>();
        foreach (int item in (IEnumerable<int>)result)
        {
            items.Add(item);
        }
        items.Should().Equal(8, 5, 3);
    }

    // Nsmallest with n == len(iterable): returns all in ascending order

    [Fact]
    public void Nsmallest_NEqualsListLength_ReturnsAllAscending()
    {
        IList<int> data = new List<int> { 5, 3, 8 };
        var result = Sharpy.Heapq.Nsmallest(3, data);
        var items = new List<int>();
        foreach (int item in (IEnumerable<int>)result)
        {
            items.Add(item);
        }
        items.Should().Equal(3, 5, 8);
    }

    // Nlargest/Nsmallest with duplicate values: preserves all instances

    [Fact]
    public void Nlargest_WithDuplicates_PreservesAllInstances()
    {
        IList<int> data = new List<int> { 5, 5, 3, 3, 1 };
        var result = Sharpy.Heapq.Nlargest(3, data);
        var items = new List<int>();
        foreach (int item in (IEnumerable<int>)result)
        {
            items.Add(item);
        }
        items.Should().Equal(5, 5, 3);
    }

    [Fact]
    public void Nsmallest_WithDuplicates_PreservesAllInstances()
    {
        IList<int> data = new List<int> { 5, 5, 3, 3, 1 };
        var result = Sharpy.Heapq.Nsmallest(3, data);
        var items = new List<int>();
        foreach (int item in (IEnumerable<int>)result)
        {
            items.Add(item);
        }
        items.Should().Equal(1, 3, 3);
    }

    // Heappop drains heap to empty and then throws

    [Fact]
    public void Heappop_AfterDraining_ThrowsIndexError()
    {
        var h = new List<int> { 1 };
        Sharpy.Heapq.Heappop(h);  // removes last element
        FluentActions.Invoking(() => Sharpy.Heapq.Heappop(h))
            .Should().Throw<Sharpy.IndexError>();
    }
}
