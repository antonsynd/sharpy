using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class DivMod_Tests
{
    [Fact]
    public void DivMod_PositiveIntegers_ReturnsQuotientAndRemainder()
    {
        // When
        var (quotient, remainder) = DivMod(17, 5);

        // Then
        quotient.Should().Be(3);
        remainder.Should().Be(2);
    }

    [Fact]
    public void DivMod_NegativeDividend_ReturnsCorrectQuotientAndRemainder()
    {
        // When
        var (quotient, remainder) = DivMod(-17, 5);

        // Then (Python floored division: -17 // 5 = -4, -17 % 5 = 3)
        quotient.Should().Be(-4);
        remainder.Should().Be(3);
    }

    [Fact]
    public void DivMod_NegativeDivisor_ReturnsCorrectQuotientAndRemainder()
    {
        // When
        var (quotient, remainder) = DivMod(17, -5);

        // Then (Python floored division: 17 // -5 = -4, 17 % -5 = -3)
        quotient.Should().Be(-4);
        remainder.Should().Be(-3);
    }

    [Fact]
    public void DivMod_ZeroDivisor_ThrowsDivideByZeroException()
    {
        // When/Then
        FluentActions.Invoking(() => DivMod(10, 0))
            .Should().Throw<DivideByZeroException>();
    }

    [Fact]
    public void DivMod_Doubles_ReturnsQuotientAndRemainder()
    {
        // When
        var (quotient, remainder) = DivMod(17.5, 5.0);

        // Then
        quotient.Should().BeApproximately(3.0, 0.001);
        remainder.Should().BeApproximately(2.5, 0.001);
    }
}
