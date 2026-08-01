using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// #1216 — the decimal <c>//</c> and <c>%</c> zero guards moved out of an emitted ternary
/// (which spliced the divisor twice, so a side-effecting divisor ran twice) and into these
/// Core helpers. The parity theory below pins the helpers against the exact expressions the
/// emitter used to produce, so the move is provably value-preserving; the ground-truth
/// theories pin them against python3's <c>decimal</c> module.
/// </summary>
public class DecimalOps_Tests
{
    // Ground truth (python3, decimal module): Decimal(-7) // Decimal(3) is -2 (TRUNCATED
    // toward zero), where int -7 // 3 is -3 (floored).
    public static TheoryData<decimal, decimal, decimal> FloorDivCases => new()
    {
        { 7m, 3m, 2m },
        { -7m, 3m, -2m },
        { 7m, -3m, -2m },
        { -7m, -3m, 2m },
        { -17m, 5m, -3m },
        { 0m, 3m, 0m },
        { 7.5m, 2m, 3m },
        { -7.5m, 2m, -3m },
    };

    // Ground truth (python3): Decimal(-7) % Decimal(3) is -1 — the sign of the DIVIDEND,
    // where int -7 % 3 is 2 (sign of the divisor).
    public static TheoryData<decimal, decimal, decimal> ModCases => new()
    {
        { 7m, 3m, 1m },
        { -7m, 3m, -1m },
        { 7m, -3m, 1m },
        { -7m, -3m, -1m },
        { -17m, 5m, -2m },
        { 0m, 3m, 0m },
        { 7.5m, 2m, 1.5m },
        { -7.5m, 2m, -1.5m },
    };

    [Theory]
    [MemberData(nameof(FloorDivCases))]
    public void DecimalFloorDiv_MatchesPython(decimal x, decimal y, decimal expected)
    {
        DecimalFloorDiv(x, y).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ModCases))]
    public void DecimalMod_MatchesPython(decimal x, decimal y, decimal expected)
    {
        DecimalMod(x, y).Should().Be(expected);
    }

    // The helpers must reproduce the previously emitted expressions exactly — that is what
    // makes #1216 a lowering change and not a semantic one.
    [Theory]
    [MemberData(nameof(FloorDivCases))]
    public void DecimalFloorDiv_IsValueIdenticalToThePreviousEmittedShape(decimal x, decimal y, decimal expected)
    {
        DecimalFloorDiv(x, y).Should().Be(decimal.Truncate(decimal.Divide(x, y)));
        DecimalFloorDiv(x, y).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ModCases))]
    public void DecimalMod_IsValueIdenticalToThePreviousEmittedShape(decimal x, decimal y, decimal expected)
    {
        DecimalMod(x, y).Should().Be(decimal.Remainder(x, y));
        DecimalMod(x, y).Should().Be(expected);
    }

    // The divmod identity holds for the truncating pair too: x == (x // y) * y + (x % y).
    [Theory]
    [MemberData(nameof(FloorDivCases))]
    public void TruncatingDivmodIdentityHolds(decimal x, decimal y, decimal expected)
    {
        _ = expected;
        (DecimalFloorDiv(x, y) * y + DecimalMod(x, y)).Should().Be(x);
    }

    // The two zero-divisor exception types are DIFFERENT and deliberately so (#1189):
    // CPython's decimal.DivisionByZero (raised by //) IS a ZeroDivisionError, while
    // decimal.InvalidOperation (raised by %) is a sibling. Do not unify them.
    [Fact]
    public void DecimalFloorDiv_ZeroDivisor_ThrowsZeroDivisionError()
    {
        var act = () => DecimalFloorDiv(7m, 0m);
        act.Should().Throw<ZeroDivisionError>().WithMessage("decimal floor division by zero");
    }

    [Fact]
    public void DecimalMod_ZeroDivisor_ThrowsInvalidOperation()
    {
        var act = () => DecimalMod(7m, 0m);
        act.Should().Throw<InvalidOperation>().WithMessage("decimal modulo by zero");
    }

    [Fact]
    public void DecimalMod_ZeroDivisor_IsNotAZeroDivisionError()
    {
        var act = () => DecimalMod(7m, 0m);
        act.Should().NotThrow<ZeroDivisionError>();
        act.Should().Throw<ArithmeticError>();
    }

    // A zero DIVIDEND never trips either guard.
    [Fact]
    public void ZeroDividend_DoesNotThrow()
    {
        DecimalFloorDiv(0m, 3m).Should().Be(0m);
        DecimalMod(0m, 3m).Should().Be(0m);
    }
}
