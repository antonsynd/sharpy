// TODO: Temporarily disabled due to API mismatch with itertools implementation
#if ITERTOOLS_ADDITIONAL_TESTS_ENABLED
using Xunit;
using FluentAssertions;
using System.Linq;

namespace Sharpy.Core.Tests;

public class ItertoolsAdditional_Tests
{
    // --- Count ---

    [Fact]
    public void Count_DefaultStart_YieldsFromZero()
    {
        var c = new Sharpy.CountIterator(0, 1);
        c.Next().Should().Be(0);
        c.Next().Should().Be(1);
        c.Next().Should().Be(2);
    }

    [Fact]
    public void Count_WithStartAndStep()
    {
        var c = new Sharpy.CountIterator(10, 2);
        c.Next().Should().Be(10);
        c.Next().Should().Be(12);
        c.Next().Should().Be(14);
        c.Next().Should().Be(16);
        c.Next().Should().Be(18);
    }

    // --- Accumulate ---

    [Fact]
    public void Accumulate_DefaultSum()
    {
        var acc = new Sharpy.AccumulateIterator<int>(
            new[] { 1, 2, 3, 4, 5 }, null, default, false);
        var results = new System.Collections.Generic.List<int>();
        while (acc.MoveNext()) results.Add(acc.Current);
        results.Should().Equal(1, 3, 6, 10, 15);
    }

    [Fact]
    public void Accumulate_WithFunc()
    {
        var acc = new Sharpy.AccumulateIterator<int>(
            new[] { 1, 2, 3, 4, 5 }, (a, b) => a * b, default, false);
        var results = new System.Collections.Generic.List<int>();
        while (acc.MoveNext()) results.Add(acc.Current);
        results.Should().Equal(1, 2, 6, 24, 120);
    }

    [Fact]
    public void Accumulate_WithInitial()
    {
        var acc = new Sharpy.AccumulateIterator<int>(
            new[] { 1, 2, 3 }, null, 100, true);
        var results = new System.Collections.Generic.List<int>();
        while (acc.MoveNext()) results.Add(acc.Current);
        results.Should().Equal(100, 101, 103, 106);
    }

    [Fact]
    public void Accumulate_EmptyIterable()
    {
        var acc = new Sharpy.AccumulateIterator<int>(
            System.Array.Empty<int>(), null, default, false);
        acc.MoveNext().Should().BeFalse();
    }

    // --- Dropwhile ---

    [Fact]
    public void Dropwhile_DropsWhileTrue()
    {
        var dw = new Sharpy.DropwhileIterator<int>(x => x < 5, new[] { 1, 4, 6, 4, 1 });
        var results = new System.Collections.Generic.List<int>();
        while (dw.MoveNext()) results.Add(dw.Current);
        results.Should().Equal(6, 4, 1);
    }

    [Fact]
    public void Dropwhile_NeverTrue_YieldsAll()
    {
        var dw = new Sharpy.DropwhileIterator<int>(x => x > 100, new[] { 1, 2, 3 });
        var results = new System.Collections.Generic.List<int>();
        while (dw.MoveNext()) results.Add(dw.Current);
        results.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Dropwhile_AlwaysTrue_YieldsNothing()
    {
        var dw = new Sharpy.DropwhileIterator<int>(x => x < 100, new[] { 1, 2, 3 });
        var results = new System.Collections.Generic.List<int>();
        while (dw.MoveNext()) results.Add(dw.Current);
        results.Should().BeEmpty();
    }

    // --- Takewhile ---

    [Fact]
    public void Takewhile_TakesWhileTrue()
    {
        var tw = new Sharpy.TakewhileIterator<int>(x => x < 5, new[] { 1, 4, 6, 4, 1 });
        var results = new System.Collections.Generic.List<int>();
        while (tw.MoveNext()) results.Add(tw.Current);
        results.Should().Equal(1, 4);
    }

    [Fact]
    public void Takewhile_AlwaysTrue_YieldsAll()
    {
        var tw = new Sharpy.TakewhileIterator<int>(x => x < 100, new[] { 1, 2, 3 });
        var results = new System.Collections.Generic.List<int>();
        while (tw.MoveNext()) results.Add(tw.Current);
        results.Should().Equal(1, 2, 3);
    }

    // --- Compress ---

    [Fact]
    public void Compress_SelectsMatchingElements()
    {
        var c = new Sharpy.CompressIterator<string>(
            new[] { "A", "B", "C", "D", "E", "F" },
            new[] { true, false, true, false, true, true });
        var results = new System.Collections.Generic.List<string>();
        while (c.MoveNext()) results.Add(c.Current);
        results.Should().Equal("A", "C", "E", "F");
    }

    [Fact]
    public void Compress_UnevenLengths_StopsAtShorter()
    {
        var c = new Sharpy.CompressIterator<int>(
            new[] { 1, 2, 3, 4, 5 },
            new[] { true, true });
        var results = new System.Collections.Generic.List<int>();
        while (c.MoveNext()) results.Add(c.Current);
        results.Should().Equal(1, 2);
    }

    // --- Filterfalse ---

    [Fact]
    public void Filterfalse_FiltersWherePredicateFalse()
    {
        var ff = new Sharpy.FilterfalseIterator<int>(x => x % 2 != 0, new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        var results = new System.Collections.Generic.List<int>();
        while (ff.MoveNext()) results.Add(ff.Current);
        results.Should().Equal(0, 2, 4, 6, 8);
    }

    // --- ZipLongest ---

    [Fact]
    public void ZipLongest_FillsShorterIterables()
    {
        var zl = new Sharpy.ZipLongestIterator<string>(
            new IEnumerable<string>[] { new[] { "A", "B" }, new[] { "x", "y", "z" } },
            "-");
        var results = new System.Collections.Generic.List<string[]>();
        while (zl.MoveNext()) results.Add(zl.Current);
        results.Should().HaveCount(3);
        results[0].Should().Equal("A", "x");
        results[1].Should().Equal("B", "y");
        results[2].Should().Equal("-", "z");
    }

    // --- Pairwise ---

    [Fact]
    public void Pairwise_ReturnsSlidingPairs()
    {
        var pw = new Sharpy.PairwiseIterator<int>(new[] { 1, 2, 3, 4, 5 });
        var results = new System.Collections.Generic.List<(int, int)>();
        while (pw.MoveNext()) results.Add(pw.Current);
        results.Should().Equal((1, 2), (2, 3), (3, 4), (4, 5));
    }

    [Fact]
    public void Pairwise_SingleElement_YieldsNothing()
    {
        var pw = new Sharpy.PairwiseIterator<int>(new[] { 1 });
        pw.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Pairwise_Empty_YieldsNothing()
    {
        var pw = new Sharpy.PairwiseIterator<int>(System.Array.Empty<int>());
        pw.MoveNext().Should().BeFalse();
    }

    // --- Product ---

    [Fact]
    public void Product_TwoIterables_ReturnsCartesianProduct()
    {
        var p = new Sharpy.ProductIterator<string>(
            new IEnumerable<string>[] { new[] { "A", "B" }, new[] { "1", "2" } });
        var results = new System.Collections.Generic.List<string[]>();
        while (p.MoveNext()) results.Add(p.Current);
        results.Should().HaveCount(4);
        results[0].Should().Equal("A", "1");
        results[1].Should().Equal("A", "2");
        results[2].Should().Equal("B", "1");
        results[3].Should().Equal("B", "2");
    }

    [Fact]
    public void Product_EmptyPool_YieldsNothing()
    {
        var p = new Sharpy.ProductIterator<int>(
            new IEnumerable<int>[] { new[] { 1, 2 }, System.Array.Empty<int>() });
        p.MoveNext().Should().BeFalse();
    }

    // --- Groupby ---

    [Fact]
    public void Groupby_ConsecutiveGroups()
    {
        var gb = new Sharpy.GroupbyIterator<char, char>(
            "AAABBBCC".ToCharArray(), x => x);
        var results = new System.Collections.Generic.List<(char, int)>();
        while (gb.MoveNext())
        {
            results.Add((gb.Current.Key, ((System.Collections.Generic.ICollection<char>)gb.Current.Group).Count));
        }
        results.Should().Equal(('A', 3), ('B', 3), ('C', 2));
    }

    // --- CombinationsWithReplacement ---

    [Fact]
    public void CombinationsWithReplacement_ReturnsCorrect()
    {
        var cwr = new Sharpy.CombinationsWithReplacementIterator<char>(new[] { 'A', 'B' }, 2);
        var results = new System.Collections.Generic.List<char[]>();
        while (cwr.MoveNext()) results.Add(cwr.Current);
        results.Should().HaveCount(3);
        results[0].Should().Equal('A', 'A');
        results[1].Should().Equal('A', 'B');
        results[2].Should().Equal('B', 'B');
    }

    [Fact]
    public void CombinationsWithReplacement_RZero_ReturnsSingleEmpty()
    {
        var cwr = new Sharpy.CombinationsWithReplacementIterator<int>(new[] { 1, 2, 3 }, 0);
        cwr.MoveNext().Should().BeTrue();
        cwr.Current.Should().BeEmpty();
        cwr.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void CombinationsWithReplacement_NegativeR_ThrowsValueError()
    {
        FluentActions.Invoking(() => new Sharpy.CombinationsWithReplacementIterator<int>(new[] { 1 }, -1))
            .Should().Throw<Sharpy.ValueError>();
    }
}
#endif
