using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ItertoolsFilter_Tests
{
    // --- Compress ---

    [Fact]
    public void Compress_IntData_FiltersCorrectly()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Compress(new[] { 1, 2, 3, 4 }, new[] { true, false, true, false }))
        {
            result.Add(n);
        }

        // Python: list(itertools.compress([1,2,3,4],[True,False,True,False])) == [1,3]
        result.Should().Equal(1, 3);
    }

    [Fact]
    public void Compress_EmptyDataAndSelectors_ReturnsEmpty()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Compress(Array.Empty<int>(), Array.Empty<bool>()))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compress_ExtraSelectors_IgnoresExtra()
    {
        var result = new List<int>();
        // Python: list(itertools.compress([1,2,3],[True,True,True,True])) == [1,2,3]
        // Extra selectors beyond data length are ignored
        foreach (int n in Itertools.Compress(new[] { 1, 2, 3 }, new[] { true, true, true, true }))
        {
            result.Add(n);
        }

        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Compress_AllSelectorsFalse_ReturnsEmpty()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Compress(new[] { 1, 2, 3 }, new[] { false, false, false }))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    // --- Dropwhile ---

    [Fact]
    public void Dropwhile_EmptyIterable_ReturnsEmpty()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Dropwhile(x => x < 5, Array.Empty<int>()))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Dropwhile_PredicateAlwaysTrue_ReturnsEmpty()
    {
        var result = new List<int>();
        // Python: list(itertools.dropwhile(lambda x: True, [1,2,3])) == []
        foreach (int n in Itertools.Dropwhile(_ => true, new[] { 1, 2, 3 }))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    // --- Takewhile ---

    [Fact]
    public void Takewhile_EmptyIterable_ReturnsEmpty()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Takewhile(x => x < 5, Array.Empty<int>()))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Takewhile_PredicateAlwaysFalse_ReturnsEmpty()
    {
        var result = new List<int>();
        // Python: list(itertools.takewhile(lambda x: False, [1,2,3])) == []
        foreach (int n in Itertools.Takewhile(_ => false, new[] { 1, 2, 3 }))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Takewhile_StopsAtFirstFalse()
    {
        var result = new List<int>();
        // Even though later elements satisfy predicate, stops at first false
        // Python: list(itertools.takewhile(lambda x: x < 5, [1,4,6,4,1])) == [1, 4]
        foreach (int n in Itertools.Takewhile(x => x < 5, new[] { 1, 4, 6, 4, 1 }))
        {
            result.Add(n);
        }

        result.Should().Equal(1, 4);
    }

    // --- Filterfalse ---

    [Fact]
    public void Filterfalse_EmptyIterable_ReturnsEmpty()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Filterfalse(x => x < 5, Array.Empty<int>()))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Filterfalse_KeepsElementsWhenPredicateFalse()
    {
        var result = new List<int>();
        // Python: list(itertools.filterfalse(lambda x: x < 5, [1,4,6,4,1])) == [6]
        foreach (int n in Itertools.Filterfalse(x => x < 5, new[] { 1, 4, 6, 4, 1 }))
        {
            result.Add(n);
        }

        result.Should().Equal(6);
    }

    [Fact]
    public void Filterfalse_AllPredicateFalse_ReturnsAll()
    {
        var result = new List<int>();
        // Python: list(itertools.filterfalse(lambda x: x > 100, [1,2,3])) == [1,2,3]
        foreach (int n in Itertools.Filterfalse(x => x > 100, new[] { 1, 2, 3 }))
        {
            result.Add(n);
        }

        result.Should().Equal(1, 2, 3);
    }

    // --- Islice ---

    [Fact]
    public void Islice_StopZero_ReturnsEmpty()
    {
        var result = new List<int>();
        // Python: list(itertools.islice([1,2,3,4,5], 0)) == []
        foreach (int n in Itertools.Islice(new[] { 1, 2, 3, 4, 5 }, 0))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Islice_StopBeyondSource_ReturnsAllElements()
    {
        var result = new List<int>();
        // Python: list(itertools.islice([1,2,3], 100)) == [1,2,3]
        foreach (int n in Itertools.Islice(new[] { 1, 2, 3 }, 100))
        {
            result.Add(n);
        }

        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Islice_StartAndStop_YieldsCorrectSubsequence()
    {
        var result = new List<int>();
        // Python: list(itertools.islice(range(10), 2, 5)) == [2,3,4]
        foreach (int n in Itertools.Islice(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 2, 5))
        {
            result.Add(n);
        }

        result.Should().Equal(2, 3, 4);
    }

    [Fact]
    public void Islice_WithStep2_SkipsEveryOther()
    {
        var result = new List<int>();
        // Python: list(itertools.islice(range(10), 0, 10, 2)) == [0,2,4,6,8]
        foreach (int n in Itertools.Islice(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 0, 10, 2))
        {
            result.Add(n);
        }

        result.Should().Equal(0, 2, 4, 6, 8);
    }
}
