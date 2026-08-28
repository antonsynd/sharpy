using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class FloorMod_Tests
{
    // Python floored modulo: the result takes the sign of the divisor.
    // Ground truth verified via python3 (Batch G / #1153).
    [Theory]
    [InlineData(7, 3, 1)]
    [InlineData(-7, 3, 2)]      // sign of divisor, not -1
    [InlineData(7, -3, -2)]
    [InlineData(-7, -3, -1)]
    [InlineData(17, 5, 2)]
    [InlineData(-17, 5, 3)]
    [InlineData(17, -5, -3)]
    [InlineData(0, 5, 0)]
    [InlineData(6, 3, 0)]       // exact division → 0
    [InlineData(-6, 3, 0)]
    [InlineData(6, -3, 0)]
    public void FloorMod_Int_MatchesPython(int x, int y, int expected)
    {
        FloorMod(x, y).Should().Be(expected);
    }

    [Theory]
    [InlineData(7L, 3L, 1L)]
    [InlineData(-7L, 3L, 2L)]
    [InlineData(7L, -3L, -2L)]
    [InlineData(-7L, -3L, -1L)]
    [InlineData(0L, 5L, 0L)]
    [InlineData(6L, 3L, 0L)]
    [InlineData(-6L, 3L, 0L)]
    [InlineData(9_000_000_000L, 7L, 5L)]           // large magnitude, no overflow in r += y
    [InlineData(-9_000_000_000L, 7L, 2L)]
    public void FloorMod_Long_MatchesPython(long x, long y, long expected)
    {
        FloorMod(x, y).Should().Be(expected);
    }

    [Theory]
    [InlineData(-7.5, 2.0, 0.5)]
    [InlineData(7.5, -2.0, -0.5)]
    [InlineData(-7.5, -2.0, -1.5)]
    [InlineData(7.5, 2.0, 1.5)]
    [InlineData(-7.0, 3.0, 2.0)]    // integer-valued doubles still floor
    [InlineData(7.0, -3.0, -2.0)]
    [InlineData(6.0, 3.0, 0.0)]     // exact → 0
    public void FloorMod_Double_MatchesPython(double x, double y, double expected)
    {
        FloorMod(x, y).Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData(-7.5f, 2.0f, 0.5f)]
    [InlineData(7.5f, -2.0f, -0.5f)]
    [InlineData(-7.5f, -2.0f, -1.5f)]
    [InlineData(7.5f, 2.0f, 1.5f)]
    [InlineData(6.0f, 3.0f, 0.0f)]
    public void FloorMod_Float_MatchesPython(float x, float y, float expected)
    {
        FloorMod(x, y).Should().BeApproximately(expected, 1e-6f);
    }

    [Fact]
    public void FloorMod_IntZeroDivisor_ThrowsZeroDivisionError()
    {
        FluentActions.Invoking(() => FloorMod(1, 0))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("integer modulo by zero");
    }

    [Fact]
    public void FloorMod_LongZeroDivisor_ThrowsZeroDivisionError()
    {
        FluentActions.Invoking(() => FloorMod(1L, 0L))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("integer modulo by zero");
    }

    [Fact]
    public void FloorMod_DoubleZeroDivisor_ThrowsZeroDivisionError()
    {
        FluentActions.Invoking(() => FloorMod(1.0, 0.0))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("float modulo");
    }

    [Fact]
    public void FloorMod_FloatZeroDivisor_ThrowsZeroDivisionError()
    {
        FluentActions.Invoking(() => FloorMod(1.0f, 0.0f))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("float modulo");
    }

    [Fact]
    public void FloorMod_TinyNonzeroDivisor_DoesNotRaise()
    {
        // Python raises ZeroDivisionError only for an exact-zero divisor, not tiny ones.
        FluentActions.Invoking(() => FloorMod(1.0, 1e-300))
            .Should().NotThrow();
    }

    // Coherence with Builtins.Divmod: the remainder component must equal FloorMod
    // (both implement Python's floored semantics).
    [Theory]
    [InlineData(7, 3)]
    [InlineData(-7, 3)]
    [InlineData(7, -3)]
    [InlineData(-7, -3)]
    [InlineData(17, 5)]
    [InlineData(-17, 5)]
    [InlineData(0, 5)]
    public void FloorMod_IntAgreesWithDivmodRemainder(int x, int y)
    {
        var (_, remainder) = Divmod(x, y);
        FloorMod(x, y).Should().Be(remainder);
    }

    [Theory]
    [InlineData(-7.5, 2.0)]
    [InlineData(7.5, -2.0)]
    [InlineData(-7.5, -2.0)]
    [InlineData(7.5, 2.0)]
    public void FloorMod_DoubleAgreesWithDivmodRemainder(double x, double y)
    {
        var (_, remainder) = Divmod(x, y);
        FloorMod(x, y).Should().BeApproximately(remainder, 1e-9);
    }

    // #1662 — ulong overload: both operands non-negative, so floor mod = truncating mod.
    [Theory]
    [InlineData(7UL, 3UL, 1UL)]
    [InlineData(7UL, 2UL, 1UL)]
    [InlineData(0UL, 5UL, 0UL)]
    [InlineData(6UL, 3UL, 0UL)]
    [InlineData(ulong.MaxValue, 2UL, 1UL)]
    public void FloorMod_ULong_MatchesTruncation(ulong x, ulong y, ulong expected)
    {
        FloorMod(x, y).Should().Be(expected);
    }

    [Fact]
    public void FloorMod_ULong_ZeroDivisor_ThrowsZeroDivisionError()
    {
        FluentActions.Invoking(() => FloorMod(1UL, 0UL))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("integer modulo by zero");
    }

    // #1186 — a zero remainder carries the DIVISOR's sign (CPython float_mod), where
    // C#'s `%` gives it the dividend's sign. Asserted via IsNegative because
    // -0.0 == 0.0 makes an equality assertion blind to the difference.
    // Ground truth (python3): -1.0 % 1.0 -> 0.0, 1.0 % -1.0 -> -0.0,
    //                          1.0 % 1.0 -> 0.0, -1.0 % -1.0 -> -0.0,
    //                         -2.0 % 0.5 -> 0.0, 2.0 % -0.5 -> -0.0
    [Theory]
    [InlineData(-1.0, 1.0, false)]
    [InlineData(1.0, -1.0, true)]
    [InlineData(1.0, 1.0, false)]
    [InlineData(-1.0, -1.0, true)]
    [InlineData(-2.0, 0.5, false)]
    [InlineData(2.0, -0.5, true)]
    public void FloorMod_DoubleZeroRemainder_TakesDivisorSign(double x, double y, bool expectNegativeZero)
    {
        var r = FloorMod(x, y);

        r.Should().Be(0.0);
        double.IsNegative(r).Should().Be(expectNegativeZero);
    }

    [Theory]
    [InlineData(-1.0f, 1.0f, false)]
    [InlineData(1.0f, -1.0f, true)]
    [InlineData(1.0f, 1.0f, false)]
    [InlineData(-1.0f, -1.0f, true)]
    [InlineData(-2.0f, 0.5f, false)]
    [InlineData(2.0f, -0.5f, true)]
    public void FloorMod_FloatZeroRemainder_TakesDivisorSign(float x, float y, bool expectNegativeZero)
    {
        var r = FloorMod(x, y);

        r.Should().Be(0.0f);
        float.IsNegative(r).Should().Be(expectNegativeZero);
    }

    // Divmod delegates its remainder to FloorMod, so it inherits the divisor-signed zero.
    // Ground truth (python3): divmod(-1.0, 1.0) -> (-1.0, 0.0); divmod(1.0, -1.0) -> (-1.0, -0.0)
    [Theory]
    [InlineData(-1.0, 1.0, false)]
    [InlineData(1.0, -1.0, true)]
    public void Divmod_DoubleZeroRemainder_InheritsDivisorSign(double x, double y, bool expectNegativeZero)
    {
        var (_, remainder) = Divmod(x, y);

        remainder.Should().Be(0.0);
        double.IsNegative(remainder).Should().Be(expectNegativeZero);
    }

    [Theory]
    [InlineData(-1.0f, 1.0f, false)]
    [InlineData(1.0f, -1.0f, true)]
    public void Divmod_FloatZeroRemainder_InheritsDivisorSign(float x, float y, bool expectNegativeZero)
    {
        var (_, remainder) = Divmod(x, y);

        remainder.Should().Be(0.0f);
        float.IsNegative(remainder).Should().Be(expectNegativeZero);
    }
}
