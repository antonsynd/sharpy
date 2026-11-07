using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// <c>round()</c> per CPython <c>float.__round__</c> / <c>int.__round__</c> (#2460), on the overloads the compiled
/// differential matrix (<c>RoundDifferentialTests</c>) does not reach: the <c>float</c> (single) and <c>decimal</c>
/// overloads, CPython's digit-range edges, and the 32-bit / 64-bit range refusals. Expected values were read from
/// python3 3.13 (<c>decimal.Decimal</c> for the decimal rows; a single's exact binary value for the float rows).
/// </summary>
public class RoundPythonContract_Tests
{
    // ── double: the digit-range edges of float.__round__ ──

    [Fact]
    public void Round_DoubleDigitsPastPrecision_ReturnsX() => Round(5e-324, 400).Should().Be(5e-324);

    [Fact]
    public void Round_DoubleDigitsBelowExponentRange_ReturnsSignedZero()
    {
        var r = Round(-2.5, -400);
        r.Should().Be(0.0);
        double.IsNegative(r).Should().BeTrue();
    }

    [Fact]
    public void Round_DoubleRoundedPastMax_RaisesOverflowError() =>
        FluentActions.Invoking(() => Round(double.MaxValue, -308)).Should().Throw<OverflowError>()
            .WithMessage("rounded value too large to represent");

    [Fact]
    public void Round_DoubleNegativeTieToZero_KeepsSign() => double.IsNegative(Round(-0.5, 0)).Should().BeTrue();

    [Fact]
    public void Round_DoubleNearInt32Edges_RoundsExactly()
    {
        Round(2147483646.5).Should().Be(2147483646);
        Round(-2147483648.4).Should().Be(int.MinValue);
    }

    [Fact]
    public void Round_DoublePastInt32_RaisesOverflowError() =>
        FluentActions.Invoking(() => Round(2147483647.5)).Should().Throw<OverflowError>();

    // ── float (single) ──

    [Fact]
    public void Round_SingleCorrectlyRounded_UsesExactBinaryValue()
    {
        // (float)2.675 is 2.6749999523162841796875: below the tie.
        Round(2.675f, 2).Should().Be(2.67f);
        Round(0.125f, 2).Should().Be(0.12f);
        Round(1234.5f, -2).Should().Be(1200f);
    }

    [Fact]
    public void Round_SingleNanAndInfinity_FollowFloatRound()
    {
        float.IsNaN(Round(float.NaN, 2)).Should().BeTrue();
        float.IsPositiveInfinity(Round(float.PositiveInfinity, 2)).Should().BeTrue();
        FluentActions.Invoking(() => Round(float.NaN)).Should().Throw<ValueError>();
        FluentActions.Invoking(() => Round(float.NegativeInfinity)).Should().Throw<OverflowError>();
        FluentActions.Invoking(() => Round(1e20f)).Should().Throw<OverflowError>();
    }

    [Fact]
    public void Round_SingleRoundedPastMax_RaisesOverflowError() =>
        FluentActions.Invoking(() => Round(float.MaxValue, -35)).Should().Throw<OverflowError>();

    // ── decimal: exact, half to even, any digits ──

    [Fact]
    public void Round_DecimalDigits_HalfToEvenOnTheExactValue()
    {
        Round(2.675m, 2).Should().Be(2.68m);
        Round(-0.125m, 2).Should().Be(-0.12m);
        Round(-2.5m, 0).Should().Be(-2m);
        Round(1234.5m, -2).Should().Be(1200m);
        Round(35m, -1).Should().Be(40m);
        Round(1.5m, 5).Should().Be(1.5m);
    }

    [Fact]
    public void Round_DecimalPastItsRange_RaisesOverflowError() =>
        FluentActions.Invoking(() => Round(decimal.MaxValue, -28)).Should().Throw<OverflowError>();

    [Fact]
    public void Round_DecimalToInt_HalfToEvenAndRangeChecked()
    {
        Round(2.5m).Should().Be(2);
        Round(-3.5m).Should().Be(-4);
        FluentActions.Invoking(() => Round(3000000000m)).Should().Throw<OverflowError>();
    }

    // ── int / long: int.__round__ ──

    [Fact]
    public void Round_IntNegativeDigits_HalfToEvenOnTheInteger()
    {
        Round(5, -1).Should().Be(0);
        Round(15, -1).Should().Be(20);
        Round(25, -1).Should().Be(20);
        Round(-25, -1).Should().Be(-20);
        Round(5, -400).Should().Be(0);
        Round(7, 3).Should().Be(7);
        Round(1099511627776L, -3).Should().Be(1099511628000L);
    }

    [Fact]
    public void Round_IntResultPastRange_RaisesOverflowError()
    {
        // python3 gives 2147483650 / -2147483650 / 9223372036854775810: past int32 / int64 (Axiom 1).
        FluentActions.Invoking(() => Round(int.MaxValue, -1)).Should().Throw<OverflowError>();
        FluentActions.Invoking(() => Round(int.MinValue, -1)).Should().Throw<OverflowError>();
        FluentActions.Invoking(() => Round(long.MaxValue, -1)).Should().Throw<OverflowError>();
    }
}
