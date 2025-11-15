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
        range.__Next__().Should().Be(0);
        range.__Next__().Should().Be(1);
        range.__Next__().Should().Be(2);
        range.__Next__().Should().Be(3);
        range.__Next__().Should().Be(4);

        FluentActions.Invoking(() => range.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_TwoArguments_GeneratesStartToStop()
    {
        // When
        var range = Range(2, 7);

        // Then
        range.__Next__().Should().Be(2);
        range.__Next__().Should().Be(3);
        range.__Next__().Should().Be(4);
        range.__Next__().Should().Be(5);
        range.__Next__().Should().Be(6);

        FluentActions.Invoking(() => range.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_ThreeArguments_GeneratesWithStep()
    {
        // When
        var range = Range(0, 10, 2);

        // Then
        range.__Next__().Should().Be(0);
        range.__Next__().Should().Be(2);
        range.__Next__().Should().Be(4);
        range.__Next__().Should().Be(6);
        range.__Next__().Should().Be(8);

        FluentActions.Invoking(() => range.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_NegativeStep_GeneratesDescending()
    {
        // When
        var range = Range(10, 0, -2);

        // Then
        range.__Next__().Should().Be(10);
        range.__Next__().Should().Be(8);
        range.__Next__().Should().Be(6);
        range.__Next__().Should().Be(4);
        range.__Next__().Should().Be(2);

        FluentActions.Invoking(() => range.__Next__())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_EmptyRange_ReturnsEmptyIterator()
    {
        // When
        var range = Range(5, 5);

        // Then
        FluentActions.Invoking(() => range.__Next__())
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
}
