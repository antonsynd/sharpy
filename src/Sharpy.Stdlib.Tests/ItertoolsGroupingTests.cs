using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ItertoolsGrouping_Tests
{
    // --- Groupby ---

    [Fact]
    public void Groupby_EmptyIterable_ReturnsEmpty()
    {
        var groups = new List<(int, List<int>)>();

        foreach (var (key, group) in Itertools.Groupby(new Sharpy.List<int>(), (Func<int, int>)(x => x)))
        {
            var items = new List<int>();
            foreach (int item in group)
                items.Add(item);
            groups.Add((key, items));
        }

        groups.Should().BeEmpty();
    }

    [Fact]
    public void Groupby_NonConsecutiveSameKeys_CreatesSeparateGroups()
    {
        var groups = new List<(int, List<int>)>();

        // Python: groupby([1,2,1,2]) creates 4 groups, not 2
        foreach (var (key, group) in Itertools.Groupby(new Sharpy.List<int>(new[] { 1, 2, 1, 2 }), (Func<int, int>)(x => x)))
        {
            var items = new List<int>();
            foreach (int item in group)
                items.Add(item);
            groups.Add((key, items));
        }

        groups.Should().HaveCount(4);
        groups[0].Item1.Should().Be(1);
        groups[1].Item1.Should().Be(2);
        groups[2].Item1.Should().Be(1);
        groups[3].Item1.Should().Be(2);
    }

    [Fact]
    public void Groupby_ByStringLength_GroupsStringsCorrectly()
    {
        var groups = new List<(int, List<string>)>();
        var data = new Sharpy.List<string>(new[] { "a", "ab", "b", "bc" });

        // Python: groupby([a,ab,b,bc], key=len) -> (1,[a]), (2,[ab]), (1,[b]), (2,[bc])
        foreach (var (key, group) in Itertools.Groupby(data, (Func<string, int>)(s => s.Length)))
        {
            var items = new List<string>();
            foreach (string item in group)
                items.Add(item);
            groups.Add((key, items));
        }

        groups.Should().HaveCount(4);
        groups[0].Item1.Should().Be(1);
        groups[0].Item2.Should().Equal("a");
        groups[1].Item1.Should().Be(2);
        groups[1].Item2.Should().Equal("ab");
        groups[2].Item1.Should().Be(1);
        groups[2].Item2.Should().Equal("b");
        groups[3].Item1.Should().Be(2);
        groups[3].Item2.Should().Equal("bc");
    }

    [Fact]
    public void Groupby_AllSameKey_ReturnsSingleGroup()
    {
        var groups = new List<(int, List<int>)>();

        foreach (var (key, group) in Itertools.Groupby(new Sharpy.List<int>(new[] { 5, 5, 5 }), (Func<int, int>)(x => x)))
        {
            var items = new List<int>();
            foreach (int item in group)
                items.Add(item);
            groups.Add((key, items));
        }

        groups.Should().HaveCount(1);
        groups[0].Item1.Should().Be(5);
        groups[0].Item2.Should().Equal(5, 5, 5);
    }

    // --- Pairwise ---

    [Fact]
    public void Pairwise_TwoElements_ReturnsSinglePair()
    {
        var result = new List<(int, int)>();
        foreach (var pair in Itertools.Pairwise(new Sharpy.List<int>(new[] { 10, 20 })))
        {
            result.Add(pair);
        }

        // Python: list(itertools.pairwise([10,20])) == [(10,20)]
        result.Should().HaveCount(1);
        result[0].Should().Be((10, 20));
    }

    // --- Starmap ---

    [Fact]
    public void Starmap_EmptyIterable_ReturnsEmpty()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Starmap<int, int, int>((a, b) => a + b, new Sharpy.List<(int, int)>()))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Starmap_SinglePair_ReturnsOneResult()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Starmap<int, int, int>((a, b) => a * b, new Sharpy.List<(int, int)>(new[] { (3, 7) })))
        {
            result.Add(n);
        }

        result.Should().Equal(21);
    }

    // --- ZipLongest ---

    [Fact]
    public void ZipLongest_EmptyAndNonEmpty_FillsWithDefault()
    {
        var result = new List<(int, int)>();
        // Python: list(itertools.zip_longest([], [1,2])) == [(None, 1), (None, 2)]
        // For int: default(int) == 0
        foreach (var pair in Itertools.ZipLongest(new Sharpy.List<int>(), new Sharpy.List<int>(new[] { 1, 2 }), 0))
        {
            result.Add(pair);
        }

        result.Should().HaveCount(2);
        result[0].Should().Be((0, 1));
        result[1].Should().Be((0, 2));
    }

    [Fact]
    public void ZipLongest_BothEmpty_ReturnsEmpty()
    {
        var result = new List<(int, int)>();
        foreach (var pair in Itertools.ZipLongest(new Sharpy.List<int>(), new Sharpy.List<int>(), 0))
        {
            result.Add(pair);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void ZipLongest_WithFillvalue_UsesCustomFillvalue()
    {
        var result = new List<(int, int)>();
        // Python: list(itertools.zip_longest([1,2,3],[4,5], fillvalue=99)) == [(1,4),(2,5),(3,99)]
        foreach (var pair in Itertools.ZipLongest(
            new Sharpy.List<int>(new[] { 1, 2, 3 }),
            new Sharpy.List<int>(new[] { 4, 5 }),
            99))
        {
            result.Add(pair);
        }

        result.Should().HaveCount(3);
        result[0].Should().Be((1, 4));
        result[1].Should().Be((2, 5));
        result[2].Should().Be((3, 99));
    }

    // --- Chain (static method) ---

    [Fact]
    public void Chain_MultipleIterables_ConcatenatesAll()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Chain(new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 }))
        {
            result.Add(n);
        }

        result.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void Chain_WithEmptyIterable_SkipsEmpty()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Chain(new[] { 1, 2 }, Array.Empty<int>()))
        {
            result.Add(n);
        }

        result.Should().Equal(1, 2);
    }

    [Fact]
    public void Chain_AllEmpty_ReturnsEmpty()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Chain(Array.Empty<int>(), Array.Empty<int>()))
        {
            result.Add(n);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public void Chain_SingleIterable_ReturnsAllElements()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Chain(new[] { 42 }))
        {
            result.Add(n);
        }

        result.Should().Equal(42);
    }

    // --- Accumulate (additional cases beyond ItertoolsAdditionalTests) ---

    [Fact]
    public void Accumulate_DefaultSumFunction_ReturnsCumulativeSums()
    {
        var result = new List<int>();
        // Python: list(itertools.accumulate([1,2,3,4])) == [1,3,6,10]
        foreach (int n in Itertools.Accumulate(new Sharpy.List<int>(new[] { 1, 2, 3, 4 })))
        {
            result.Add(n);
        }

        result.Should().Equal(1, 3, 6, 10);
    }
}
