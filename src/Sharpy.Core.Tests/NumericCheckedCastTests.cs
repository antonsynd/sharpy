using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// The throwing sibling of <see cref="NumericSafeCast"/> (#1306). Same range predicates, so these
/// mirror <c>NumericSafeCast_Tests</c>; what is unique here is WHICH exception comes out, because the
/// whole point of the helper is that <c>except OverflowError</c> in Sharpy can catch it —
/// <c>System.OverflowException</c>, which a C# <c>checked(...)</c> cast would throw, cannot be caught
/// from Sharpy at all.
/// </summary>
public class NumericCheckedCast_Tests
{
    // ----- the exception identity, which is the reason this class exists -----

    [Fact]
    public void OutOfRange_ThrowsSharpyOverflowError_NotSystemOverflowException()
    {
        var ex = Assert.Throws<OverflowError>(() => NumericCheckedCast.ToInt(4294967296L));

        ex.Should().NotBeOfType<System.OverflowException>();
        ex.Should().BeAssignableTo<ArithmeticError>("OverflowError sits under ArithmeticError");
    }

    [Fact]
    public void NaN_ThrowsSharpyValueError()
    {
        // CPython: int(float('nan')) raises ValueError, int(float('inf')) raises OverflowError.
        Assert.Throws<ValueError>(() => NumericCheckedCast.ToInt(double.NaN));
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToInt(double.PositiveInfinity));
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToInt(double.NegativeInfinity));
    }

    // ----- double hub -----

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(3.9, 3)]      // truncates toward zero, like Python's int(3.9)
    [InlineData(-3.9, -3)]
    [InlineData(2147483647.0, int.MaxValue)]
    [InlineData(-2147483648.0, int.MinValue)]
    public void ToInt_Double_InRange_Truncates(double input, int expected)
        => NumericCheckedCast.ToInt(input).Should().Be(expected);

    [Fact]
    public void ToInt_Double_NegativeZero_IsZero()
        => NumericCheckedCast.ToInt(-0.0).Should().Be(0);

    [Theory]
    [InlineData(2147483648.0)]
    [InlineData(-2147483649.0)]
    [InlineData(1e300)]
    public void ToInt_Double_OutOfRange_Throws(double input)
        => Assert.Throws<OverflowError>(() => NumericCheckedCast.ToInt(input));

    [Fact]
    public void ToLong_Double_UsesTheExclusive2Pow63Bound()
    {
        // long.MaxValue is NOT exactly representable as double — it rounds up to 2^63 — so the guard
        // must be a strict `< 2^63`. An inclusive `<= long.MaxValue` would admit exactly this value.
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToLong(9223372036854775808.0));

        NumericCheckedCast.ToLong(-9223372036854775808.0).Should().Be(long.MinValue);
        NumericCheckedCast.ToLong(9223372036854774784.0).Should().Be(9223372036854774784L);
    }

    [Fact]
    public void ToULong_Double_UsesTheExclusive2Pow64Bound()
    {
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToULong(18446744073709551616.0));
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToULong(-1.0));

        NumericCheckedCast.ToULong(1e18).Should().Be(1000000000000000000UL);
    }

    [Theory]
    [InlineData(255.0)]
    [InlineData(0.0)]
    public void ToByte_Double_BoundariesFit(double input)
        => NumericCheckedCast.ToByte(input).Should().Be((byte)input);

    [Theory]
    [InlineData(256.0)]
    [InlineData(-1.0)]
    [InlineData(-0.5)]  // truncation would land on 0, but the predicate is on the value (documented)
    public void ToByte_Double_OutOfRange_Throws(double input)
        => Assert.Throws<OverflowError>(() => NumericCheckedCast.ToByte(input));

    // ----- long hub -----

    [Theory]
    [InlineData(0L, 0)]
    [InlineData(2147483647L, int.MaxValue)]
    [InlineData(-2147483648L, int.MinValue)]
    public void ToInt_Long_InRange(long input, int expected)
        => NumericCheckedCast.ToInt(input).Should().Be(expected);

    [Theory]
    [InlineData(2147483648L)]
    [InlineData(-2147483649L)]
    public void ToInt_Long_OutOfRange_Throws(long input)
        => Assert.Throws<OverflowError>(() => NumericCheckedCast.ToInt(input));

    [Fact]
    public void LongHub_CoversEveryNarrowerWidth()
    {
        NumericCheckedCast.ToSByte(127L).Should().Be(127);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToSByte(128L));

        NumericCheckedCast.ToByte(255L).Should().Be(255);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToByte(-1L));

        NumericCheckedCast.ToShort(32767L).Should().Be(32767);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToShort(32768L));

        NumericCheckedCast.ToUShort(65535L).Should().Be(65535);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToUShort(-1L));

        NumericCheckedCast.ToUInt(4294967295L).Should().Be(4294967295U);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToUInt(-1L));
    }

    [Fact]
    public void ToULong_Long_FailsOnlyForNegatives()
    {
        NumericCheckedCast.ToULong(0L).Should().Be(0UL);
        NumericCheckedCast.ToULong(long.MaxValue).Should().Be(9223372036854775807UL);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToULong(-1L));
    }

    // ----- ulong hub: the one integral source with no implicit conversion to long -----

    [Fact]
    public void ToLong_ULong_RefusesAbove2Pow63Minus1()
    {
        NumericCheckedCast.ToLong(9223372036854775807UL).Should().Be(long.MaxValue);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToLong(9223372036854775808UL));
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToLong(ulong.MaxValue));
    }

    [Fact]
    public void ULongHub_CoversEveryNarrowerWidth()
    {
        NumericCheckedCast.ToSByte(127UL).Should().Be(127);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToSByte(128UL));

        NumericCheckedCast.ToByte(255UL).Should().Be(255);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToByte(256UL));

        NumericCheckedCast.ToShort(32767UL).Should().Be(32767);
        NumericCheckedCast.ToUShort(65535UL).Should().Be(65535);
        NumericCheckedCast.ToInt(2147483647UL).Should().Be(int.MaxValue);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToInt(2147483648UL));
        NumericCheckedCast.ToUInt(4294967295UL).Should().Be(4294967295U);
        Assert.Throws<OverflowError>(() => NumericCheckedCast.ToUInt(4294967296UL));
    }

    // ----- lockstep with the failable sibling -----

    [Theory]
    [InlineData(4294967296.0)]
    [InlineData(-4294967296.0)]
    [InlineData(1e300)]
    [InlineData(double.NaN)]
    public void ThrowsExactlyWhereTheSafeSiblingReturnsNone(double input)
    {
        // The two helpers must agree on WHICH values fail — only the failure action differs.
        NumericSafeCast.ToIntOrNone(input).IsNone.Should().BeTrue();
        Assert.ThrowsAny<Exception>(() => NumericCheckedCast.ToInt(input));
    }

    [Theory]
    [InlineData(3.9)]
    [InlineData(-3.9)]
    [InlineData(0.0)]
    [InlineData(2147483647.0)]
    public void AgreesWithTheSafeSiblingOnSuccessfulValues(double input)
    {
        NumericSafeCast.ToIntOrNone(input).Unwrap().Should().Be(NumericCheckedCast.ToInt(input));
    }
}
