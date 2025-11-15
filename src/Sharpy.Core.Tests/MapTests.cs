using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Map_Tests
{
    [Fact]
    public void Map_EmptyList_ReturnsEmptyIterator()
    {
        // Given
        var list = new List<int>();

        // When
        var mapped = Map(x => x * 2, list);

        // Then
        FluentActions.Invoking(() => mapped.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Map_TransformIntegers_ReturnsTransformedElements()
    {
        // Given
        List<int> list = [1, 2, 3, 4];

        // When
        var mapped = Map(x => x * 2, list);

        // Then
        mapped.__Next__().Should().Be(2);
        mapped.__Next__().Should().Be(4);
        mapped.__Next__().Should().Be(6);
        mapped.__Next__().Should().Be(8);

        FluentActions.Invoking(() => mapped.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Map_TransformToString_ReturnsStringElements()
    {
        // Given
        List<int> list = [1, 2, 3];

        // When
        var mapped = Map(x => $"Number {x}", list);

        // Then
        mapped.__Next__().Should().Be("Number 1");
        mapped.__Next__().Should().Be("Number 2");
        mapped.__Next__().Should().Be("Number 3");

        FluentActions.Invoking(() => mapped.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Map_NullFunction_ThrowsTypeError()
    {
        // Given
        var list = new List<int>();

        // When/Then
        FluentActions.Invoking(() => Map<int, int>(null!, list))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Map_NullIterable_ThrowsTypeError()
    {
        // When/Then
        FluentActions.Invoking(() => Map<int, int>(x => x, null!))
            .Should().Throw<TypeError>();
    }
}
