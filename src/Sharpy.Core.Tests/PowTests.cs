using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

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

    [Theory]
    [InlineData(2, 10, 1024)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 5, 0)]
    [InlineData(1, 100, 1)]
    [InlineData(5, 3, 125)]
    [InlineData(-2, 3, -8)]
    [InlineData(-2, 4, 16)]
    public void CheckedIntPow_Int_InRange_ReturnsExactResult(int x, int y, int expected)
    {
        // When/then
        CheckedIntPow(x, y).Should().Be(expected);
    }

    [Fact]
    public void CheckedIntPow_Long_ExactResultBeyond2Pow53()
    {
        // 3 ** 39 = 4052555153018976267, exact in long but not in double
        CheckedIntPow(3L, 39L).Should().Be(4052555153018976267L);
    }

    [Fact]
    public void CheckedIntPow_Int_Overflow_ThrowsOverflowError()
    {
        // 2 ** 31 overflows int (max 2^31 - 1)
        System.Action act = () => CheckedIntPow(2, 31);

        act.Should().Throw<OverflowError>();
    }

    [Fact]
    public void CheckedIntPow_Long_Overflow_ThrowsOverflowError()
    {
        // 2 ** 63 overflows long (max 2^63 - 1)
        System.Action act = () => CheckedIntPow(2L, 63L);

        act.Should().Throw<OverflowError>();
    }

    [Fact]
    public void CheckedIntPow_Int_NegativeExponent_ThrowsArgumentOutOfRange()
    {
        System.Action act = () => CheckedIntPow(2, -1);

        act.Should().Throw<System.ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CheckedIntPow_Long_NegativeExponent_ThrowsArgumentOutOfRange()
    {
        System.Action act = () => CheckedIntPow(2L, -1L);

        act.Should().Throw<System.ArgumentOutOfRangeException>();
    }
}
