using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Range_Tests
{
    [Fact]
    public void Range_SingleArgument_GeneratesZeroToStop()
    {
        // When
        var range = Range(5);

        // Then
        range.Next().Should().Be(0);
        range.Next().Should().Be(1);
        range.Next().Should().Be(2);
        range.Next().Should().Be(3);
        range.Next().Should().Be(4);

        FluentActions.Invoking(() => range.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_TwoArguments_GeneratesStartToStop()
    {
        // When
        var range = Range(2, 7);

        // Then
        range.Next().Should().Be(2);
        range.Next().Should().Be(3);
        range.Next().Should().Be(4);
        range.Next().Should().Be(5);
        range.Next().Should().Be(6);

        FluentActions.Invoking(() => range.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_ThreeArguments_GeneratesWithStep()
    {
        // When
        var range = Range(0, 10, 2);

        // Then
        range.Next().Should().Be(0);
        range.Next().Should().Be(2);
        range.Next().Should().Be(4);
        range.Next().Should().Be(6);
        range.Next().Should().Be(8);

        FluentActions.Invoking(() => range.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_NegativeStep_GeneratesDescending()
    {
        // When
        var range = Range(10, 0, -2);

        // Then
        range.Next().Should().Be(10);
        range.Next().Should().Be(8);
        range.Next().Should().Be(6);
        range.Next().Should().Be(4);
        range.Next().Should().Be(2);

        FluentActions.Invoking(() => range.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_EmptyRange_ReturnsEmptyIterator()
    {
        // When
        var range = Range(5, 5);

        // Then
        FluentActions.Invoking(() => range.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_ZeroStep_ThrowsValueError()
    {
        // When/Then
        FluentActions.Invoking(() => Range(0, 10, 0))
            .Should().Throw<ValueError>()
            .WithMessage("*step*zero*");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(-3, 0)]
    public void Range_Count_SingleArgument_MatchesLen(int stop, int expected)
    {
        // Count matches Python's len(range(stop)) and is readable without enumerating.
        ((ISized)Range(stop)).Count.Should().Be(expected);
    }

    [Theory]
    [InlineData(2, 5, 1, 3)]     // 2,3,4
    [InlineData(0, 10, 3, 4)]    // 0,3,6,9
    [InlineData(5, 0, -1, 5)]    // 5,4,3,2,1
    [InlineData(10, 0, -3, 4)]   // 10,7,4,1
    [InlineData(0, 10, -1, 0)]   // empty (wrong-direction step)
    public void Range_Count_StartStopStep_MatchesLen(int start, int stop, int step, int expected)
    {
        ((ISized)Range(start, stop, step)).Count.Should().Be(expected);
    }

    [Fact]
    public void Range_Count_DoesNotConsumeIterator()
    {
        // Reading Count must not advance the iterator.
        var range = Range(3);
        ((ISized)range).Count.Should().Be(3);
        range.Next().Should().Be(0);
        range.Next().Should().Be(1);
    }

    // ── Contains: O(1) arithmetic membership matching CPython (#1778) ──

    [Theory]
    [InlineData(0, 10, 1, 3, true)]
    [InlineData(0, 10, 1, 0, true)]
    [InlineData(0, 10, 1, 9, true)]
    [InlineData(0, 10, 1, 10, false)]   // exclusive stop
    [InlineData(0, 10, 1, -1, false)]
    [InlineData(0, 10, 2, 4, true)]     // on step
    [InlineData(0, 10, 2, 5, false)]    // not on step
    [InlineData(0, 10, 3, 6, true)]
    [InlineData(0, 10, 3, 9, true)]
    [InlineData(0, 10, 3, 7, false)]
    [InlineData(10, 0, -1, 5, true)]    // negative step
    [InlineData(10, 0, -1, 10, true)]
    [InlineData(10, 0, -1, 0, false)]   // exclusive stop
    [InlineData(10, 0, -3, 10, true)]
    [InlineData(10, 0, -3, 7, true)]
    [InlineData(10, 0, -3, 4, true)]
    [InlineData(10, 0, -3, 1, true)]
    [InlineData(10, 0, -3, 8, false)]   // not on step
    [InlineData(0, 0, 1, 0, false)]     // empty range
    [InlineData(5, 5, 1, 5, false)]
    [InlineData(10, 0, 1, 5, false)]    // empty: wrong direction
    public void Range_Contains_MatchesCPython(int start, int stop, int step, int value, bool expected)
    {
        new RangeIterator(start, stop, step).Contains(value).Should().Be(expected);
    }

    [Fact]
    public void Range_Contains_DoesNotConsumeIterator()
    {
        var range = Range(5);
        range.Contains(3).Should().BeTrue();
        range.Next().Should().Be(0, "Contains must not advance the iterator");
    }
}
