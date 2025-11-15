using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public class Pow_Tests
{
    [Fact]
    public void Pow_PositiveIntegers_ReturnsCorrectPower()
    {
        // When
        var result = Pow(2, 3);

        // Then
        result.Should().BeApproximately(8.0, 0.001);
    }

    [Fact]
    public void Pow_Doubles_ReturnsCorrectPower()
    {
        // When
        var result = Pow(2.0, 3.0);

        // Then
        result.Should().BeApproximately(8.0, 0.001);
    }

    [Fact]
    public void Pow_FractionalExponent_ReturnsSquareRoot()
    {
        // When
        var result = Pow(9.0, 0.5);

        // Then
        result.Should().BeApproximately(3.0, 0.001);
    }

    [Fact]
    public void Pow_ZeroExponent_ReturnsOne()
    {
        // When
        var result = Pow(5, 0);

        // Then
        result.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void Pow_NegativeExponent_ReturnsReciprocal()
    {
        // When
        var result = Pow(2.0, -2.0);

        // Then
        result.Should().BeApproximately(0.25, 0.001);
    }
}
