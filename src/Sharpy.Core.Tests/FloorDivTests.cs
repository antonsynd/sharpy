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
