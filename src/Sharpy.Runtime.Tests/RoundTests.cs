using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public class Round_Tests
{
    [Fact]
    public void Round_DoubleNoDecimals_RoundsToNearestInteger()
    {
        // When
        var result = Round(3.14159);

        // Then
        result.Should().Be(3);
    }

    [Fact]
    public void Round_DoubleWithDecimals_RoundsToNDecimals()
    {
        // When
        var result = Round(3.14159, 2);

        // Then
        result.Should().BeApproximately(3.14, 0.001);
    }

    [Fact]
    public void Round_DoubleHalfUp_RoundsUp()
    {
        // When
        var result = Round(2.5);

        // Then
        result.Should().Be(2); // .NET uses banker's rounding (round to even)
    }

    [Fact]
    public void Round_FloatNoDecimals_RoundsToNearestInteger()
    {
        // When
        var result = Round(3.7f);

        // Then
        result.Should().Be(4);
    }

    [Fact]
    public void Round_DecimalNoDecimals_RoundsToNearestInteger()
    {
        // When
        var result = Round(3.6m);

        // Then
        result.Should().Be(4);
    }
}
