using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class FloorDiv_Tests
{
    // #1185 — Math.Floor(x / y) diverges from CPython whenever x / y rounds up across
    // an integer boundary. Ground truth verified via python3.
    [Theory]
    [InlineData(1.0, 0.1, 9.0)]      // Math.Floor(1.0 / 0.1) would give 10.0
    [InlineData(7.5, 0.1, 74.0)]     // Math.Floor(7.5 / 0.1) would give 75.0
    [InlineData(-1.0, 0.1, -10.0)]
    [InlineData(1.0, -0.1, -10.0)]
    [InlineData(-1.0, -0.1, 9.0)]
    [InlineData(-7.5, 2.0, -4.0)]
    [InlineData(7.5, 2.0, 3.0)]
    [InlineData(7.0, 3.0, 2.0)]
    [InlineData(-7.0, 3.0, -3.0)]
    [InlineData(6.0, 3.0, 2.0)]
    public void FloorDiv_Double_MatchesPython(double x, double y, double expected)
    {
        FloorDiv(x, y).Should().Be(expected);
    }

    [Theory]
    [InlineData(1.0f, 0.1f, 9.0f)]
    [InlineData(7.5f, 0.1f, 74.0f)]
    [InlineData(7.5f, 2.5f, 3.0f)]
    [InlineData(-7.5f, 2.0f, -4.0f)]
    [InlineData(-1.0f, -0.1f, 9.0f)]
    public void FloorDiv_Float_MatchesPython(float x, float y, float expected)
    {
        FloorDiv(x, y).Should().Be(expected);
    }

    // CPython's float_divmod gives a zero quotient the sign of the TRUE quotient, not the
    // sign Math.Floor preserves. Asserted via IsNegative because -0.0 == 0.0.
    // Ground truth (python3): -0.5 // -1.0 -> 0.0, -0.0 // 1.0 -> -0.0, 0.0 // -1.0 -> -0.0
    [Theory]
    [InlineData(-0.5, -1.0, false)]  // Math.Floor(-0.0) would give -0.0
    [InlineData(0.5, 1.0, false)]
    [InlineData(-0.0, 1.0, true)]
    [InlineData(0.0, -1.0, true)]
    [InlineData(-0.25, -0.5, false)]
    public void FloorDiv_DoubleZeroQuotient_TakesTrueQuotientSign(double x, double y, bool expectNegativeZero)
    {
        var q = FloorDiv(x, y);

        q.Should().Be(0.0);
        double.IsNegative(q).Should().Be(expectNegativeZero);
    }

    [Theory]
    [InlineData(-0.5f, -1.0f, false)]
    [InlineData(0.5f, 1.0f, false)]
    [InlineData(-0.0f, 1.0f, true)]
    [InlineData(0.0f, -1.0f, true)]
    public void FloorDiv_FloatZeroQuotient_TakesTrueQuotientSign(float x, float y, bool expectNegativeZero)
    {
        var q = FloorDiv(x, y);

        q.Should().Be(0.0f);
        float.IsNegative(q).Should().Be(expectNegativeZero);
    }

    [Fact]
    public void FloorDiv_DoubleZeroDivisor_ThrowsZeroDivisionError()
    {
        FluentActions.Invoking(() => FloorDiv(1.0, 0.0))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("float floor division by zero");
    }

    [Fact]
    public void FloorDiv_FloatZeroDivisor_ThrowsZeroDivisionError()
    {
        FluentActions.Invoking(() => FloorDiv(1.0f, 0.0f))
            .Should().Throw<ZeroDivisionError>()
            .WithMessage("float floor division by zero");
    }

    [Fact]
    public void FloorDiv_TinyNonzeroDivisor_DoesNotRaise()
    {
        // Python raises only for an exact-zero divisor, not tiny ones.
        FluentActions.Invoking(() => FloorDiv(1.0, 1e-300))
            .Should().NotThrow();
    }

    // The identity #1153 established for integers now holds for floats too — this is the
    // property Math.Floor(x / y) broke (1.0 // 0.1 == 10.0 makes the right side 1.09...).
    [Theory]
    [InlineData(1.0, 0.1)]
    [InlineData(7.5, 0.1)]
    [InlineData(-1.0, 0.1)]
    [InlineData(1.0, -0.1)]
    [InlineData(-7.5, 2.0)]
    [InlineData(17.5, 5.0)]
    public void FloorDiv_SatisfiesDivmodIdentity(double x, double y)
    {
        var reconstructed = (FloorDiv(x, y) * y) + FloorMod(x, y);

        reconstructed.Should().BeApproximately(x, 1e-9);
    }

    // Divmod's quotient is FloorDiv by construction (CPython implements float_floor_div as
    // float_divmod's first element); this pins the two together.
    [Theory]
    [InlineData(1.0, 0.1)]
    [InlineData(7.5, 0.1)]
    [InlineData(-7.5, 2.0)]
    [InlineData(7.5, -2.0)]
    [InlineData(-7.5, -2.0)]
    public void FloorDiv_AgreesWithDivmodQuotient(double x, double y)
    {
        var (quotient, _) = Divmod(x, y);

        FloorDiv(x, y).Should().Be(quotient);
    }

    [Theory]
    [InlineData(1.0f, 0.1f)]
    [InlineData(-7.5f, 2.0f)]
    public void FloorDiv_Float_AgreesWithDivmodQuotient(float x, float y)
    {
        var (quotient, _) = Divmod(x, y);

        FloorDiv(x, y).Should().Be(quotient);
    }

    [Fact]
    public void FloorDiv_NaNOperand_PropagatesNaN()
    {
        // Python: float('nan') // 1.0 -> nan
        double.IsNaN(FloorDiv(double.NaN, 1.0)).Should().BeTrue();
    }

    [Fact]
    public void FloorDiv_InfiniteDivisor_FloorsTowardNegativeInfinity()
    {
        // Python: 5.0 // inf -> 0.0, -5.0 // inf -> -1.0
        FloorDiv(5.0, double.PositiveInfinity).Should().Be(0.0);
        FloorDiv(-5.0, double.PositiveInfinity).Should().Be(-1.0);
    }
}

/// <summary>
/// The integer <c>FloorDiv</c> overloads added in #1226.
/// </summary>
/// <remarks>
/// Every expected value here was produced by running CPython 3.12.13, not derived by hand —
/// <c>python3 -c "print(-7 // 3)"</c> and friends. The one case where Sharpy deliberately
/// diverges from CPython (<c>int.MinValue // -1</c>) is called out at its test.
/// </remarks>
public class IntegerFloorDiv_Tests
{
    // The sign matrix. C# `/` truncates toward zero; Python floors. The two disagree on
    // every row where the signs differ and the division is inexact.
    [Theory]
    [InlineData(7, 3, 2)]
    [InlineData(-7, 3, -3)]     // truncating division would give -2
    [InlineData(7, -3, -3)]     // truncating division would give -2
    [InlineData(-7, -3, 2)]
    [InlineData(6, 3, 2)]       // exact: signs differ but no adjustment
    [InlineData(-6, 3, -2)]
    [InlineData(6, -3, -2)]
    [InlineData(0, 5, 0)]
    [InlineData(1, 2, 0)]
    [InlineData(-1, 2, -1)]     // truncating division would give 0
    [InlineData(1, -2, -1)]
    public void FloorDiv_Int_MatchesPython(int x, int y, int expected)
    {
        FloorDiv(x, y).Should().Be(expected);
    }

    [Theory]
    [InlineData(7L, 3L, 2L)]
    [InlineData(-7L, 3L, -3L)]
    [InlineData(7L, -3L, -3L)]
    [InlineData(-7L, -3L, 2L)]
    public void FloorDiv_Long_MatchesPython(long x, long y, long expected)
    {
        FloorDiv(x, y).Should().Be(expected);
    }

    // The reason this helper computes in integer arithmetic instead of
    // (long)Math.Floor((double)x / y): above 2^53 a double cannot represent the operands, so
    // the round-trip returns a neighbouring value. These are the exact CPython quotients.
    [Theory]
    [InlineData(4611686018427387905L, 3L, 1537228672809129301L)]   // (2**62 + 1) // 3
    [InlineData(4611686018427387905L, 7L, 658812288346769700L)]    // (2**62 + 1) // 7
    [InlineData(9007199254740993L, 3L, 3002399751580331L)]         // (2**53 + 1) // 3
    [InlineData(9223372036854775807L, 2L, 4611686018427387903L)]   // long.MaxValue // 2
    [InlineData(-9223372036854775808L, 2L, -4611686018427387904L)] // long.MinValue // 2
    public void FloorDiv_Long_IsExactAboveDoublePrecision(long x, long y, long expected)
    {
        FloorDiv(x, y).Should().Be(expected);
    }

    [Fact]
    public void FloorDiv_Int_ZeroDivisor_RaisesZeroDivisionError()
    {
        var act = () => FloorDiv(1, 0);

        act.Should().Throw<ZeroDivisionError>()
            .WithMessage("integer division or modulo by zero");
    }

    [Fact]
    public void FloorDiv_Long_ZeroDivisor_RaisesZeroDivisionError()
    {
        var act = () => FloorDiv(1L, 0L);

        act.Should().Throw<ZeroDivisionError>()
            .WithMessage("integer division or modulo by zero");
    }

    // DELIBERATE CPython divergence, decided rather than inherited. CPython computes
    // -2**31 // -1 == 2147483648 exactly, because its integers are arbitrary precision.
    // That value does not fit an int, and .NET raises OverflowException for int.MinValue / -1
    // even in an unchecked context (a hardware trap, unlike * and +, which wrap) — so there is
    // no "match the runtime wrap" option to fall back on. Diagnosing matches CheckedIntPow's
    // contract. The behavior this replaces returned int.MaxValue: silently wrong.
    [Fact]
    public void FloorDiv_Int_MinValueByMinusOne_RaisesOverflowError()
    {
        var act = () => FloorDiv(int.MinValue, -1);

        act.Should().Throw<OverflowError>()
            .WithMessage("integer division result too large for int");
    }

    [Fact]
    public void FloorDiv_Long_MinValueByMinusOne_RaisesOverflowError()
    {
        var act = () => FloorDiv(long.MinValue, -1L);

        act.Should().Throw<OverflowError>()
            .WithMessage("integer division result too large for long");
    }

    // The divmod identity from #1153: x == (x // y) * y + (x % y). This is the property that
    // ties FloorDiv to FloorMod, and it is why FloorDiv borrows Divmod's algorithm rather than
    // inventing one. Swept over the full sign grid rather than sampled.
    [Fact]
    public void FloorDiv_Int_SatisfiesTheDivmodIdentity()
    {
        for (int x = -8; x <= 8; x++)
        {
            for (int y = -8; y <= 8; y++)
            {
                if (y == 0)
                {
                    continue;
                }

                (FloorDiv(x, y) * y + FloorMod(x, y)).Should().Be(
                    x, "the divmod identity must hold for {0} // {1}", x, y);
            }
        }
    }

    // FloorDiv must agree with Divmod's quotient everywhere — they share an algorithm precisely
    // so the two cannot drift apart.
    [Fact]
    public void FloorDiv_Int_AgreesWithDivmodQuotient()
    {
        for (int x = -8; x <= 8; x++)
        {
            for (int y = -8; y <= 8; y++)
            {
                if (y == 0)
                {
                    continue;
                }

                FloorDiv(x, y).Should().Be(Divmod(x, y).Item1);
            }
        }
    }
}
