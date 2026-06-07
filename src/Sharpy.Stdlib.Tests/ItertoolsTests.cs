using System.Linq;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Itertools_Tests
{
    // --- Chain ---

    [Fact]
    public void Chain_ConcatenatesMultipleIterables()
    {
        var result = Sharpy.Itertools.Chain(
            new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 }
        ).ToList();

        result.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void Chain_EmptyIterables_ReturnsEmpty()
    {
        var result = Sharpy.Itertools.Chain(
            System.Array.Empty<int>(), System.Array.Empty<int>()
        ).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Chain_SingleIterable_BehavesLikeOriginal()
    {
        var result = Sharpy.Itertools.Chain(
            new[] { 10, 20, 30 }
        ).ToList();

        result.Should().Equal(10, 20, 30);
    }

    [Fact]
    public void Chain_WithEmptyIntermediateIterable_SkipsIt()
    {
        var result = Sharpy.Itertools.Chain(
            new[] { 1 }, System.Array.Empty<int>(), new[] { 2 }
        ).ToList();

        result.Should().Equal(1, 2);
    }

    // --- Islice (generated methods via bridge) ---

    [Fact]
    public void Islice_StopOnly_TakesFirstNElements()
    {
        var source = new Sharpy.List<int>(new[] { 10, 20, 30, 40, 50 });
        var result = Sharpy.Itertools.Islice(source, 3).ToList();

        result.Should().HaveCount(3);
        result[0].Should().Be(10);
        result[1].Should().Be(20);
        result[2].Should().Be(30);
    }

    [Fact]
    public void Islice_StartAndStop_SkipsToStart()
    {
        var source = new Sharpy.List<int>(new[] { 10, 20, 30, 40, 50 });
        var result = Sharpy.Itertools.IsliceRange(source, 1, 4).ToList();

        result.Should().HaveCount(3);
        result[0].Should().Be(20);
        result[1].Should().Be(30);
        result[2].Should().Be(40);
    }

    [Fact]
    public void Islice_WithStep_SkipsElements()
    {
        var source = new Sharpy.List<int>(new[] { 10, 20, 30, 40, 50, 60 });
        var result = Sharpy.Itertools.IsliceRange(source, 0, 6, 2).ToList();

        result.Should().HaveCount(3);
        result[0].Should().Be(10);
        result[1].Should().Be(30);
        result[2].Should().Be(50);
    }

    [Fact]
    public void Islice_NegativeStart_ReturnsEmpty()
    {
        var source = new Sharpy.List<int>(new[] { 1 });
        var result = Sharpy.Itertools.IsliceRange(source, -1, 5).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Islice_ZeroStep_YieldsOnlyMatchingIndex()
    {
        var source = new Sharpy.List<int>(new[] { 1, 2, 3 });
        var result = Sharpy.Itertools.IsliceRange(source, 0, 5, 0).ToList();

        // With step=0, nextYield stays at 0 after first match, but i increments past it
        result.Should().HaveCount(1);
        result[0].Should().Be(1);
    }

    [Fact]
    public void Islice_StartBeyondSource_ReturnsEmpty()
    {
        var source = new Sharpy.List<int>(new[] { 1, 2 });
        var result = Sharpy.Itertools.IsliceRange(source, 10, 20).ToList();

        result.Should().BeEmpty();
    }

    // --- Combinations ---

    [Fact]
    public void Combinations_ReturnsCorrectCombinations()
    {
        var items = new Sharpy.List<int>(new[] { 1, 2, 3 });
        var results = Sharpy.Itertools.Combinations(items, 2).ToList();

        results.Should().HaveCount(3);
        results[0].Should().Equal(1, 2);
        results[1].Should().Equal(1, 3);
        results[2].Should().Equal(2, 3);
    }

    [Fact]
    public void Combinations_RLargerThanPool_ReturnsEmpty()
    {
        var items = new Sharpy.List<int>(new[] { 1, 2 });
        var results = Sharpy.Itertools.Combinations(items, 5).ToList();

        results.Should().BeEmpty();
    }

    [Fact]
    public void Combinations_RZero_ReturnsSingleEmptyList()
    {
        var items = new Sharpy.List<int>(new[] { 1, 2, 3 });
        var results = Sharpy.Itertools.Combinations(items, 0).ToList();

        results.Should().HaveCount(1);
        results[0].Should().BeEmpty();
    }

    [Fact]
    public void Combinations_NegativeR_ThrowsValueError()
    {
        var items = new Sharpy.List<int>(new[] { 1 });
        FluentActions.Invoking(() => Sharpy.Itertools.Combinations(items, -1).ToList())
            .Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Combinations_REqualsPoolSize_ReturnsSingleCombination()
    {
        var items = new Sharpy.List<int>(new[] { 1, 2, 3 });
        var results = Sharpy.Itertools.Combinations(items, 3).ToList();

        results.Should().HaveCount(1);
        results[0].Should().Equal(1, 2, 3);
    }

    // --- Permutations ---

    [Fact]
    public void Permutations_DefaultR_ReturnsFullPermutations()
    {
        var items = new Sharpy.List<int>(new[] { 1, 2, 3 });
        var results = Sharpy.Itertools.Permutations(items).ToList();

        // 3! = 6
        results.Should().HaveCount(6);
        results[0].Should().Equal(1, 2, 3);
        results[1].Should().Equal(1, 3, 2);
    }

    [Fact]
    public void Permutations_WithR_ReturnsRLengthPermutations()
    {
        var items = new Sharpy.List<int>(new[] { 1, 2, 3 });
        var results = Sharpy.Itertools.Permutations(items, 2).ToList();

        // P(3,2) = 6
        results.Should().HaveCount(6);
    }

    [Fact]
    public void Permutations_RLargerThanPool_ReturnsEmpty()
    {
        var items = new Sharpy.List<int>(new[] { 1, 2 });
        var results = Sharpy.Itertools.Permutations(items, 5).ToList();

        results.Should().BeEmpty();
    }

    [Fact]
    public void Permutations_NegativeR_ReturnsFullPermutations()
    {
        // In the generated code, r < 0 means full length (not an error)
        var items = new Sharpy.List<int>(new[] { 1, 2 });
        var results = Sharpy.Itertools.Permutations(items, -1).ToList();

        // 2! = 2
        results.Should().HaveCount(2);
    }

    [Fact]
    public void Permutations_SingleElement_ReturnsSinglePermutation()
    {
        var items = new Sharpy.List<int>(new[] { 42 });
        var results = Sharpy.Itertools.Permutations(items).ToList();

        results.Should().HaveCount(1);
        results[0].Should().Equal(42);
    }
}
