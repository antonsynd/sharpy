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
}
