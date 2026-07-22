using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class NumericSafeCast_Tests
{
    // ----- double -> int -----

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(3.9, 3)]      // truncates toward zero
    [InlineData(-3.9, -3)]    // negative truncation toward zero (matches Python int(-3.9))
    [InlineData(2147483647.0, int.MaxValue)]   // exact upper boundary
    [InlineData(-2147483648.0, int.MinValue)]  // exact lower boundary
    public void ToIntOrNone_Double_InRange_ReturnsSomeTruncated(double input, int expected)
    {
        var result = NumericSafeCast.ToIntOrNone(input);

        result.IsSome.Should().BeTrue();
        result.Unwrap().Should().Be(expected);
    }

    [Fact]
    public void ToIntOrNone_Double_NegativeZero_ReturnsSomeZero()
    {
        // -0.0 is not NaN and lies within range; truncation yields 0 (edge case from the plan).
        var result = NumericSafeCast.ToIntOrNone(-0.0);

        result.IsSome.Should().BeTrue();
        result.Unwrap().Should().Be(0);
    }

    [Theory]
    [InlineData(2147483648.0)]   // int.MaxValue + 1
    [InlineData(-2147483649.0)]  // int.MinValue - 1
    [InlineData(1e300)]
    [InlineData(-1e300)]
    public void ToIntOrNone_Double_OutOfRange_ReturnsNone(double input)
    {
        NumericSafeCast.ToIntOrNone(input).IsNone.Should().BeTrue();
    }

    [Fact]
    public void ToIntOrNone_Double_NaNAndInfinities_ReturnNone()
    {
        NumericSafeCast.ToIntOrNone(double.NaN).IsNone.Should().BeTrue();
        NumericSafeCast.ToIntOrNone(double.PositiveInfinity).IsNone.Should().BeTrue();
        NumericSafeCast.ToIntOrNone(double.NegativeInfinity).IsNone.Should().BeTrue();
    }

    // ----- float -> int -----

    [Theory]
    [InlineData(3.5f, 3)]
    [InlineData(-3.5f, -3)]
    public void ToIntOrNone_Float_InRange_ReturnsSomeTruncated(float input, int expected)
    {
        var result = NumericSafeCast.ToIntOrNone(input);

        result.IsSome.Should().BeTrue();
        result.Unwrap().Should().Be(expected);
    }

    [Fact]
    public void ToIntOrNone_Float_OutOfRangeAndNaN_ReturnNone()
    {
        // 3e9f is well above int.MaxValue; the float overload widens to double before checking, so a
        // float that rounds to a value outside int range is correctly rejected rather than overflowing.
        NumericSafeCast.ToIntOrNone(3e9f).IsNone.Should().BeTrue();
        NumericSafeCast.ToIntOrNone(-3e9f).IsNone.Should().BeTrue();
        NumericSafeCast.ToIntOrNone(float.NaN).IsNone.Should().BeTrue();
        NumericSafeCast.ToIntOrNone(float.PositiveInfinity).IsNone.Should().BeTrue();
    }

    // ----- long -> int -----

    [Theory]
    [InlineData(0L, 0)]
    [InlineData(2147483647L, int.MaxValue)]
    [InlineData(-2147483648L, int.MinValue)]
    public void ToIntOrNone_Long_InRange_ReturnsSome(long input, int expected)
    {
        var result = NumericSafeCast.ToIntOrNone(input);

        result.IsSome.Should().BeTrue();
        result.Unwrap().Should().Be(expected);
    }

    [Theory]
    [InlineData(2147483648L)]        // int.MaxValue + 1
    [InlineData(-2147483649L)]       // int.MinValue - 1
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void ToIntOrNone_Long_OutOfRange_ReturnsNone(long input)
    {
        NumericSafeCast.ToIntOrNone(input).IsNone.Should().BeTrue();
    }

    // ----- double -> long -----

    [Theory]
    [InlineData(0.0, 0L)]
    [InlineData(9.5, 9L)]
    [InlineData(-9.5, -9L)]
    [InlineData(-9223372036854775808.0, long.MinValue)] // exact lower boundary (-2^63)
    public void ToLongOrNone_Double_InRange_ReturnsSomeTruncated(double input, long expected)
    {
        var result = NumericSafeCast.ToLongOrNone(input);

        result.IsSome.Should().BeTrue();
        result.Unwrap().Should().Be(expected);
    }

    [Fact]
    public void ToLongOrNone_Double_LargestRepresentableBelow2Pow63_ReturnsSome()
    {
        // The greatest double strictly below 2^63 is 2^63 - 1024 = 9223372036854774784.0. It must be
        // accepted (it fits in long), while 2^63 itself must be rejected — the exact non-representability
        // trap the strict `< 2^63` guard is designed for.
        const double justBelow = 9223372036854774784.0;

        var result = NumericSafeCast.ToLongOrNone(justBelow);
        result.IsSome.Should().BeTrue();
        result.Unwrap().Should().Be(9223372036854774784L);
    }

    [Theory]
    [InlineData(9223372036854775808.0)]   // exactly 2^63 — one past long range
    [InlineData(-9223372036854779904.0)]  // below -2^63
    [InlineData(1e300)]
    public void ToLongOrNone_Double_OutOfRange_ReturnsNone(double input)
    {
        NumericSafeCast.ToLongOrNone(input).IsNone.Should().BeTrue();
    }

    [Fact]
    public void ToLongOrNone_Double_NaNAndInfinities_ReturnNone()
    {
        NumericSafeCast.ToLongOrNone(double.NaN).IsNone.Should().BeTrue();
        NumericSafeCast.ToLongOrNone(double.PositiveInfinity).IsNone.Should().BeTrue();
        NumericSafeCast.ToLongOrNone(double.NegativeInfinity).IsNone.Should().BeTrue();
    }

    // ----- float -> long -----

    [Theory]
    [InlineData(9.5f, 9L)]
    [InlineData(-9.5f, -9L)]
    public void ToLongOrNone_Float_InRange_ReturnsSomeTruncated(float input, long expected)
    {
        var result = NumericSafeCast.ToLongOrNone(input);

        result.IsSome.Should().BeTrue();
        result.Unwrap().Should().Be(expected);
    }

    [Fact]
    public void ToLongOrNone_Float_NaNAndInfinity_ReturnNone()
    {
        NumericSafeCast.ToLongOrNone(float.NaN).IsNone.Should().BeTrue();
        NumericSafeCast.ToLongOrNone(float.PositiveInfinity).IsNone.Should().BeTrue();
    }
}
