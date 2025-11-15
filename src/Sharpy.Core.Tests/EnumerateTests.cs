using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Enumerate_Tests
{
    [Fact]
    public void Enumerate_EmptyList_ReturnsEmptyIterator()
    {
        // Given
        var list = new List<string>();

        // When
        var enumerated = Enumerate(list);

        // Then
        FluentActions.Invoking(() => enumerated.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Enumerate_ListOfStrings_ReturnsIndexValuePairs()
    {
        // Given
        List<string> list = ["a", "b", "c"];

        // When
        var enumerated = Enumerate(list);

        // Then
        var (index0, value0) = enumerated.__Next__();
        index0.Should().Be(0);
        value0.Should().Be("a");

        var (index1, value1) = enumerated.__Next__();
        index1.Should().Be(1);
        value1.Should().Be("b");

        var (index2, value2) = enumerated.__Next__();
        index2.Should().Be(2);
        value2.Should().Be("c");

        FluentActions.Invoking(() => enumerated.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Enumerate_WithStartIndex_StartsFromGivenIndex()
    {
        // Given
        List<string> list = ["a", "b", "c"];

        // When
        var enumerated = Enumerate(list, start: 10);

        // Then
        var (index0, value0) = enumerated.__Next__();
        index0.Should().Be(10);
        value0.Should().Be("a");

        var (index1, value1) = enumerated.__Next__();
        index1.Should().Be(11);
        value1.Should().Be("b");
    }

    [Fact]
    public void Enumerate_NullIterable_ThrowsTypeError()
    {
        // When/Then
        FluentActions.Invoking(() => Enumerate<string>(null!))
            .Should().Throw<TypeError>();
    }
}
