using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Sorted_Tests
{
    [Fact]
    public void Sorted_EmptyList_ReturnsEmptyList()
    {
        // Given
        var list = new List<int>();

        // When
        var result = Sorted(list);

        // Then
        result.Should().NotBeNull();
        Len(result).Should().Be(0);
    }

    [Fact]
    public void Sorted_ListOfIntegers_ReturnsSortedList()
    {
        // Given
        List<int> list = [5, 2, 8, 1, 9, 3];

        // When
        var result = Sorted(list);

        // Then
        Len(result).Should().Be(6);
        result[0].Should().Be(1);
        result[1].Should().Be(2);
        result[2].Should().Be(3);
        result[3].Should().Be(5);
        result[4].Should().Be(8);
        result[5].Should().Be(9);
    }

    [Fact]
    public void Sorted_WithReverseTrue_ReturnsSortedDescending()
    {
        // Given
        List<int> list = [5, 2, 8, 1, 9, 3];

        // When
        var result = Sorted(list, reverse: true);

        // Then
        Len(result).Should().Be(6);
        result[0].Should().Be(9);
        result[1].Should().Be(8);
        result[2].Should().Be(5);
        result[3].Should().Be(3);
        result[4].Should().Be(2);
        result[5].Should().Be(1);
    }

    [Fact]
    public void Sorted_WithKeyFunction_SortsByKey()
    {
        // Given
        List<string> list = ["apple", "pie", "a", "longer"];

        // When
        var result = Sorted(list, key: s => s.Length);

        // Then
        Len(result).Should().Be(4);
        result[0].Should().Be("a");
        result[1].Should().Be("pie");
        result[2].Should().Be("apple");
        result[3].Should().Be("longer");
    }

    [Fact]
    public void Sorted_NullIterable_ThrowsTypeError()
    {
        // When/Then
        FluentActions.Invoking(() => Sorted<int>(null!))
            .Should().Throw<TypeError>();
    }
}
