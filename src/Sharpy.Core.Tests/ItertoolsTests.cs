using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Itertools_Tests
{
    // --- ChainIterator ---

    [Fact]
    public void Chain_ConcatenatesMultipleIterables()
    {
        var chain = new Sharpy.ChainIterator<int>(
            new IEnumerable<int>[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } }
        );

        chain.Next().Should().Be(1);
        chain.Next().Should().Be(2);
        chain.Next().Should().Be(3);
        chain.Next().Should().Be(4);
        chain.Next().Should().Be(5);

        FluentActions.Invoking(() => chain.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Chain_EmptyIterables_ThrowsStopIteration()
    {
        var chain = new Sharpy.ChainIterator<int>(
            new IEnumerable<int>[] { Array.Empty<int>(), Array.Empty<int>() }
        );

        FluentActions.Invoking(() => chain.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Chain_SingleIterable_BehavesLikeOriginal()
    {
        var chain = new Sharpy.ChainIterator<int>(
            new IEnumerable<int>[] { new[] { 10, 20, 30 } }
        );

        chain.Next().Should().Be(10);
        chain.Next().Should().Be(20);
        chain.Next().Should().Be(30);

        FluentActions.Invoking(() => chain.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Chain_WithEmptyIntermediateIterable_SkipsIt()
    {
        var chain = new Sharpy.ChainIterator<int>(
            new IEnumerable<int>[] { new[] { 1 }, Array.Empty<int>(), new[] { 2 } }
        );

        chain.Next().Should().Be(1);
        chain.Next().Should().Be(2);

        FluentActions.Invoking(() => chain.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    // --- IsliceIterator ---

    [Fact]
    public void Islice_StopOnly_TakesFirstNElements()
    {
        var source = new[] { 10, 20, 30, 40, 50 };
        var islice = new Sharpy.IsliceIterator<int>(source, 3);

        islice.Next().Should().Be(10);
        islice.Next().Should().Be(20);
        islice.Next().Should().Be(30);

        FluentActions.Invoking(() => islice.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Islice_StartAndStop_SkipsToStart()
    {
        var source = new[] { 10, 20, 30, 40, 50 };
        var islice = new Sharpy.IsliceIterator<int>(source, 1, 4);

        islice.Next().Should().Be(20);
        islice.Next().Should().Be(30);
        islice.Next().Should().Be(40);

        FluentActions.Invoking(() => islice.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Islice_WithStep_SkipsElements()
    {
        var source = new[] { 10, 20, 30, 40, 50, 60 };
        var islice = new Sharpy.IsliceIterator<int>(source, 0, 6, 2);

        islice.Next().Should().Be(10);
        islice.Next().Should().Be(30);
        islice.Next().Should().Be(50);

        FluentActions.Invoking(() => islice.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Islice_NegativeStart_ThrowsValueError()
    {
        FluentActions.Invoking(() => new Sharpy.IsliceIterator<int>(new[] { 1 }, -1, 5))
            .Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Islice_ZeroStep_ThrowsValueError()
    {
        FluentActions.Invoking(() => new Sharpy.IsliceIterator<int>(new[] { 1 }, 0, 5, 0))
            .Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Islice_StartBeyondSource_ThrowsStopIteration()
    {
        var islice = new Sharpy.IsliceIterator<int>(new[] { 1, 2 }, 10, 20);

        FluentActions.Invoking(() => islice.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    // --- CombinationsIterator ---

    [Fact]
    public void Combinations_ReturnsCorrectCombinations()
    {
        var combs = new Sharpy.CombinationsIterator<int>(new[] { 1, 2, 3 }, 2);

        combs.Next().Should().Equal(1, 2);
        combs.Next().Should().Equal(1, 3);
        combs.Next().Should().Equal(2, 3);

        FluentActions.Invoking(() => combs.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Combinations_RLargerThanPool_ThrowsStopIteration()
    {
        var combs = new Sharpy.CombinationsIterator<int>(new[] { 1, 2 }, 5);

        FluentActions.Invoking(() => combs.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Combinations_RZero_ReturnsSingleEmptyArray()
    {
        var combs = new Sharpy.CombinationsIterator<int>(new[] { 1, 2, 3 }, 0);

        combs.Next().Should().BeEmpty();

        FluentActions.Invoking(() => combs.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Combinations_NegativeR_ThrowsValueError()
    {
        FluentActions.Invoking(() => new Sharpy.CombinationsIterator<int>(new[] { 1 }, -1))
            .Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Combinations_REqualsPoolSize_ReturnsSingleCombination()
    {
        var combs = new Sharpy.CombinationsIterator<int>(new[] { 1, 2, 3 }, 3);

        combs.Next().Should().Equal(1, 2, 3);

        FluentActions.Invoking(() => combs.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    // --- PermutationsIterator ---

    [Fact]
    public void Permutations_DefaultR_ReturnsFullPermutations()
    {
        var perms = new Sharpy.PermutationsIterator<int>(new[] { 1, 2, 3 });

        var results = new List<int[]>();
        try
        {
            while (true)
            {
                results.Add(perms.Next());
            }
        }
        catch (Sharpy.StopIteration) { }

        // 3! = 6
        results.Should().HaveCount(6);
        results[0].Should().Equal(1, 2, 3);
        results[1].Should().Equal(1, 3, 2);
    }

    [Fact]
    public void Permutations_WithR_ReturnsRLengthPermutations()
    {
        var perms = new Sharpy.PermutationsIterator<int>(new[] { 1, 2, 3 }, 2);

        var results = new List<int[]>();
        try
        {
            while (true)
            {
                results.Add(perms.Next());
            }
        }
        catch (Sharpy.StopIteration) { }

        // P(3,2) = 6
        results.Should().HaveCount(6);
    }

    [Fact]
    public void Permutations_RLargerThanPool_ThrowsStopIteration()
    {
        var perms = new Sharpy.PermutationsIterator<int>(new[] { 1, 2 }, 5);

        FluentActions.Invoking(() => perms.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }

    [Fact]
    public void Permutations_NegativeR_ThrowsValueError()
    {
        FluentActions.Invoking(() => new Sharpy.PermutationsIterator<int>(new[] { 1 }, -1))
            .Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Permutations_SingleElement_ReturnsSinglePermutation()
    {
        var perms = new Sharpy.PermutationsIterator<int>(new[] { 42 });

        perms.Next().Should().Equal(42);

        FluentActions.Invoking(() => perms.Next())
            .Should().Throw<Sharpy.StopIteration>();
    }
}
