using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Filter_Tests
{
    [Fact]
    public void Filter_EmptyList_ReturnsEmptyIterator()
    {
        // Given
        var list = new List<int>();

        // When
        var filtered = Filter(x => x > 0, list);

        // Then
        FluentActions.Invoking(() => filtered.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Filter_PredicateMatchesSome_ReturnsMatchingElements()
    {
        // Given
        List<int> list = [1, 2, 3, 4, 5, 6];

        // When
        var filtered = Filter(x => x % 2 == 0, list);

        // Then
        filtered.Next().Should().Be(2);
        filtered.Next().Should().Be(4);
        filtered.Next().Should().Be(6);

        FluentActions.Invoking(() => filtered.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Filter_PredicateMatchesNone_ReturnsEmptyIterator()
    {
        // Given
        List<int> list = [1, 3, 5];

        // When
        var filtered = Filter(x => x % 2 == 0, list);

        // Then
        FluentActions.Invoking(() => filtered.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Filter_PredicateMatchesAll_ReturnsAllElements()
    {
        // Given
        List<int> list = [2, 4, 6];

        // When
        var filtered = Filter(x => x % 2 == 0, list);

        // Then
        filtered.Next().Should().Be(2);
        filtered.Next().Should().Be(4);
        filtered.Next().Should().Be(6);

        FluentActions.Invoking(() => filtered.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Filter_NullPredicate_ThrowsTypeError()
    {
        // Given
        var list = new List<int>();

        // When/Then
        FluentActions.Invoking(() => Filter<int>(null!, list))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Filter_NullIterable_ThrowsTypeError()
    {
        // When/Then
        FluentActions.Invoking(() => Filter<int>(x => true, null!))
            .Should().Throw<TypeError>();
    }
}
