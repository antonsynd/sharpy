using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Divmod_Tests
{
    [Fact]
    public void Divmod_PositiveIntegers_ReturnsQuotientAndRemainder()
    {
        // When
        var (quotient, remainder) = Divmod(17, 5);

        // Then
        quotient.Should().Be(3);
        remainder.Should().Be(2);
    }

    [Fact]
    public void Divmod_NegativeDividend_ReturnsCorrectQuotientAndRemainder()
    {
        // When
        var (quotient, remainder) = Divmod(-17, 5);

        // Then (Python floored division: -17 // 5 = -4, -17 % 5 = 3)
        quotient.Should().Be(-4);
        remainder.Should().Be(3);
    }

    [Fact]
    public void Divmod_NegativeDivisor_ReturnsCorrectQuotientAndRemainder()
    {
        // When
        var (quotient, remainder) = Divmod(17, -5);

        // Then (Python floored division: 17 // -5 = -4, 17 % -5 = -3)
        quotient.Should().Be(-4);
        remainder.Should().Be(-3);
    }

    [Fact]
    public void Divmod_ZeroDivisor_ThrowsZeroDivisionError()
    {
        // When/Then (Python: divmod(10, 0) -> ZeroDivisionError)
        FluentActions.Invoking(() => Divmod(10, 0))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("integer division or modulo by zero");
    }

    [Fact]
    public void Divmod_LongZeroDivisor_ThrowsZeroDivisionError()
    {
        // When/Then
        FluentActions.Invoking(() => Divmod(10L, 0L))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("integer division or modulo by zero");
    }

    [Fact]
    public void Divmod_DoubleZeroDivisor_ThrowsZeroDivisionErrorWithFloatDivmodMessage()
    {
        // When/Then (Python: divmod(10.0, 0.0) -> ZeroDivisionError("float divmod()"))
        // The message is divmod's own, not the "float modulo" of the delegated FloorMod.
        FluentActions.Invoking(() => Divmod(10.0, 0.0))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("float divmod()");
    }

    [Fact]
    public void Divmod_FloatZeroDivisor_ThrowsZeroDivisionErrorWithFloatDivmodMessage()
    {
        // When/Then
        FluentActions.Invoking(() => Divmod(10.0f, 0.0f))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("float divmod()");
    }

    [Theory]
    [InlineData(1e-11)]
    [InlineData(-1e-11)]
    [InlineData(double.Epsilon)]
    public void Divmod_TinyNonZeroDivisor_Computes(double y)
    {
        // Python has no epsilon tolerance: only an exact zero divisor raises.
        // When/Then
        FluentActions.Invoking(() => Divmod(1.0, y)).Should().NotThrow();
    }

    [Fact]
    public void Divmod_FloatTinyNonZeroDivisor_Computes()
    {
        // When/Then (the deleted 1e-7f epsilon raised here)
        FluentActions.Invoking(() => Divmod(1.0f, 1e-8f)).Should().NotThrow();
    }

    [Fact]
    public void Divmod_Doubles_ReturnsQuotientAndRemainder()
    {
        // When
        var (quotient, remainder) = Divmod(17.5, 5.0);

        // Then
        quotient.Should().BeApproximately(3.0, 0.001);
        remainder.Should().BeApproximately(2.5, 0.001);
    }

    [Theory]
    // Expected values from python3 divmod(); the quotient/remainder pair must match exactly.
    [InlineData(17.5, 5.0, 3.0, 2.5)]
    [InlineData(-7.5, 2.0, -4.0, 0.5)]
    [InlineData(7.5, -2.0, -4.0, -0.5)]
    [InlineData(-7.5, -2.0, 3.0, -1.5)]
    // Math.Floor(x / y) rounds the quotient up here; CPython (and now Divmod) does not.
    [InlineData(1.0, 0.1, 9.0, 0.09999999999999995)]
    [InlineData(1.0, 1e-8, 99999999.0, 9.99999997907744e-09)]
    public void Divmod_Doubles_MatchesCPythonExactly(double x, double y, double expectedQuotient, double expectedRemainder)
    {
        // When
        var (quotient, remainder) = Divmod(x, y);

        // Then
        quotient.Should().Be(expectedQuotient);
        remainder.Should().Be(expectedRemainder);
    }

    [Theory]
    // Expected values from python3 divmod(); the remainder carries the sign of the divisor.
    [InlineData(7, 2, 3, 1)]
    [InlineData(-7, 2, -4, 1)]
    [InlineData(7, -2, -4, -1)]
    [InlineData(-7, -2, 3, -1)]
    [InlineData(6, 3, 2, 0)]
    [InlineData(-6, 3, -2, 0)]
    public void Divmod_Integers_MatchesCPythonExactly(int x, int y, int expectedQuotient, int expectedRemainder)
    {
        // When
        var (quotient, remainder) = Divmod(x, y);

        // Then
        quotient.Should().Be(expectedQuotient);
        remainder.Should().Be(expectedRemainder);
    }

    [Fact]
    public void Divmod_IntegerDividendNearMinValue_DoesNotWrap()
    {
        // The quotient stays on x / y precisely because (x - remainder) / y would wrap here.
        // python3: divmod(-2147483648, 3) == (-715827883, 1)
        // When
        var (quotient, remainder) = Divmod(int.MinValue, 3);

        // Then
        quotient.Should().Be(-715827883);
        remainder.Should().Be(1);
    }
}
