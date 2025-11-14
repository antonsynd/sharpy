using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public class Zip_Tests
{
    [Fact]
    public void Zip_TwoEmptyLists_ReturnsEmptyIterator()
    {
        // Given
        var list1 = new List<int>();
        var list2 = new List<string>();

        // When
        var zipped = Zip(list1, list2);

        // Then
        FluentActions.Invoking(() => zipped.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Zip_TwoLists_ReturnsZippedPairs()
    {
        // Given
        List<int> list1 = [1, 2, 3];
        List<string> list2 = ["a", "b", "c"];

        // When
        var zipped = Zip(list1, list2);

        // Then
        var (val1_0, val2_0) = zipped.__Next__();
        val1_0.Should().Be(1);
        val2_0.Should().Be("a");

        var (val1_1, val2_1) = zipped.__Next__();
        val1_1.Should().Be(2);
        val2_1.Should().Be("b");

        var (val1_2, val2_2) = zipped.__Next__();
        val1_2.Should().Be(3);
        val2_2.Should().Be("c");

        FluentActions.Invoking(() => zipped.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Zip_DifferentLengths_StopsAtShortest()
    {
        // Given
        List<int> list1 = [1, 2, 3, 4, 5];
        List<string> list2 = ["a", "b"];

        // When
        var zipped = Zip(list1, list2);

        // Then
        var (val1_0, val2_0) = zipped.__Next__();
        val1_0.Should().Be(1);
        val2_0.Should().Be("a");

        var (val1_1, val2_1) = zipped.__Next__();
        val1_1.Should().Be(2);
        val2_1.Should().Be("b");

        FluentActions.Invoking(() => zipped.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Zip_ThreeLists_ReturnsZippedTriples()
    {
        // Given
        List<int> list1 = [1, 2];
        List<string> list2 = ["a", "b"];
        List<bool> list3 = [true, false];

        // When
        var zipped = Zip(list1, list2, list3);

        // Then
        var (val1_0, val2_0, val3_0) = zipped.__Next__();
        val1_0.Should().Be(1);
        val2_0.Should().Be("a");
        val3_0.Should().BeTrue();

        var (val1_1, val2_1, val3_1) = zipped.__Next__();
        val1_1.Should().Be(2);
        val2_1.Should().Be("b");
        val3_1.Should().BeFalse();

        FluentActions.Invoking(() => zipped.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Zip_NullFirstIterable_ThrowsTypeError()
    {
        // Given
        var list2 = new List<string>();

        // When/Then
        FluentActions.Invoking(() => Zip<int, string>(null!, list2))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Zip_NullSecondIterable_ThrowsTypeError()
    {
        // Given
        var list1 = new List<int>();

        // When/Then
        FluentActions.Invoking(() => Zip<int, string>(list1, null!))
            .Should().Throw<TypeError>();
    }
}
