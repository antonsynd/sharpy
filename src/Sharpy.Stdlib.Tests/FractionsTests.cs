using Xunit;
using FluentAssertions;
using System.Numerics;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for the fractions module Fraction class.
/// </summary>
public class FractionsTests
{
    // --- Construction ---

    [Fact]
    public void Fraction_FromInts_ReducesToLowestTerms()
    {
        var f = new Fraction(2, 4);
        f.Numerator.Should().Be(1);
        f.Denominator.Should().Be(2);
    }

    [Fact]
    public void Fraction_FromInt_HasDenominatorOne()
    {
        var f = new Fraction(5);
        f.Numerator.Should().Be(5);
        f.Denominator.Should().Be(1);
    }

    [Fact]
    public void Fraction_NegativeDenominator_NormalizesSign()
    {
        var f = new Fraction(1, -3);
        f.Numerator.Should().Be(-1);
        f.Denominator.Should().Be(3);
    }

    [Fact]
    public void Fraction_BothNegative_BecomesPositive()
    {
        var f = new Fraction(-2, -6);
        f.Numerator.Should().Be(1);
        f.Denominator.Should().Be(3);
    }

    [Fact]
    public void Fraction_ZeroDenominator_Throws()
    {
        var act = () => new Fraction(1, 0);
        act.Should().Throw<ZeroDivisionError>();
    }

    [Fact]
    public void Fraction_FromDouble_ExactRepresentation()
    {
        var f = new Fraction(0.5);
        f.Numerator.Should().Be(1);
        f.Denominator.Should().Be(2);
    }

    [Fact]
    public void Fraction_FromDouble_Quarter()
    {
        var f = new Fraction(0.25);
        f.Numerator.Should().Be(1);
        f.Denominator.Should().Be(4);
    }

    [Fact]
    public void Fraction_FromNaN_Throws()
    {
        var act = () => new Fraction(double.NaN);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Fraction_FromInfinity_Throws()
    {
        var act = () => new Fraction(double.PositiveInfinity);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Fraction_FromString_SlashFormat()
    {
        var f = new Fraction("3/7");
        f.Numerator.Should().Be(3);
        f.Denominator.Should().Be(7);
    }

    [Fact]
    public void Fraction_FromString_DecimalFormat()
    {
        var f = new Fraction("3.14");
        f.Numerator.Should().Be(157);
        f.Denominator.Should().Be(50);
    }

    [Fact]
    public void Fraction_FromString_IntegerFormat()
    {
        var f = new Fraction("42");
        f.Numerator.Should().Be(42);
        f.Denominator.Should().Be(1);
    }

    [Fact]
    public void Fraction_FromString_Negative()
    {
        var f = new Fraction("-1/3");
        f.Numerator.Should().Be(-1);
        f.Denominator.Should().Be(3);
    }

    [Fact]
    public void Fraction_FromString_NegativeDecimal()
    {
        var f = new Fraction("-0.5");
        f.Numerator.Should().Be(-1);
        f.Denominator.Should().Be(2);
    }

    [Fact]
    public void Fraction_FromString_Empty_Throws()
    {
        var act = () => new Fraction("");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Fraction_FromString_ScientificNotation()
    {
        var f = new Fraction("1e-2");
        f.Numerator.Should().Be(1);
        f.Denominator.Should().Be(100);
    }

    // --- Arithmetic ---

    [Fact]
    public void Fraction_Addition()
    {
        var result = new Fraction(1, 3) + new Fraction(1, 6);
        result.Numerator.Should().Be(1);
        result.Denominator.Should().Be(2);
    }

    [Fact]
    public void Fraction_Subtraction()
    {
        var result = new Fraction(1, 2) - new Fraction(1, 3);
        result.Numerator.Should().Be(1);
        result.Denominator.Should().Be(6);
    }

    [Fact]
    public void Fraction_Multiplication()
    {
        var result = new Fraction(2, 3) * new Fraction(3, 4);
        result.Numerator.Should().Be(1);
        result.Denominator.Should().Be(2);
    }

    [Fact]
    public void Fraction_Division()
    {
        var result = new Fraction(1, 2) / new Fraction(1, 4);
        result.Numerator.Should().Be(2);
        result.Denominator.Should().Be(1);
    }

    [Fact]
    public void Fraction_DivisionByZero_Throws()
    {
        var act = () => new Fraction(1, 2) / new Fraction(0);
        act.Should().Throw<ZeroDivisionError>();
    }

    [Fact]
    public void Fraction_Negation()
    {
        var f = -new Fraction(3, 4);
        f.Numerator.Should().Be(-3);
        f.Denominator.Should().Be(4);
    }

    [Fact]
    public void Fraction_FloorDiv()
    {
        var result = Fraction.FloorDiv(new Fraction(7, 2), new Fraction(3, 2));
        result.Should().Be(new Fraction(2));
    }

    [Fact]
    public void Fraction_FloorDiv_Negative()
    {
        var result = Fraction.FloorDiv(new Fraction(-7, 2), new Fraction(3, 2));
        result.Should().Be(new Fraction(-3));
    }

    [Fact]
    public void Fraction_Mod()
    {
        var result = Fraction.Mod(new Fraction(7, 2), new Fraction(3, 2));
        result.Should().Be(new Fraction(1, 2));
    }

    [Fact]
    public void Fraction_Pow_Positive()
    {
        var result = new Fraction(2, 3).Pow(3);
        result.Numerator.Should().Be(8);
        result.Denominator.Should().Be(27);
    }

    [Fact]
    public void Fraction_Pow_Negative()
    {
        var result = new Fraction(2, 3).Pow(-2);
        result.Numerator.Should().Be(9);
        result.Denominator.Should().Be(4);
    }

    [Fact]
    public void Fraction_Pow_Zero()
    {
        var result = new Fraction(2, 3).Pow(0);
        result.Should().Be(new Fraction(1));
    }

    // --- Comparison ---

    [Fact]
    public void Fraction_Equality()
    {
        var a = new Fraction(2, 4);
        var b = new Fraction(1, 2);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Fraction_Inequality()
    {
        var a = new Fraction(1, 3);
        var b = new Fraction(1, 2);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Fraction_LessThan()
    {
        (new Fraction(1, 3) < new Fraction(1, 2)).Should().BeTrue();
    }

    [Fact]
    public void Fraction_GreaterThan()
    {
        (new Fraction(2, 3) > new Fraction(1, 2)).Should().BeTrue();
    }

    [Fact]
    public void Fraction_CompareWithInt()
    {
        var f = new Fraction(3, 1);
        f.Equals(new Fraction(3)).Should().BeTrue();
    }

    // --- limit_denominator ---

    [Fact]
    public void Fraction_LimitDenominator_Pi()
    {
        // pi ≈ 355/113
        var pi = new Fraction("3.141592653589793");
        var approx = pi.Limit_denominator(1000);
        approx.Numerator.Should().Be(355);
        approx.Denominator.Should().Be(113);
    }

    [Fact]
    public void Fraction_LimitDenominator_AlreadyBelow()
    {
        var f = new Fraction(1, 3);
        var result = f.Limit_denominator(10);
        result.Should().Be(f);
    }

    [Fact]
    public void Fraction_LimitDenominator_InvalidMax_Throws()
    {
        var f = new Fraction(1, 3);
        var act = () => f.Limit_denominator(0);
        act.Should().Throw<ValueError>();
    }

    // --- String representation ---

    [Fact]
    public void Fraction_ToString_Fraction()
    {
        new Fraction(1, 3).ToString().Should().Be("1/3");
    }

    [Fact]
    public void Fraction_ToString_Integer()
    {
        new Fraction(4, 2).ToString().Should().Be("2");
    }

    [Fact]
    public void Fraction_ToString_Zero()
    {
        new Fraction(0, 5).ToString().Should().Be("0");
    }

    // --- Large numbers ---

    [Fact]
    public void Fraction_LargeNumeratorDenominator()
    {
        var big = BigInteger.Pow(10, 50);
        var f = new Fraction(big, big * 2);
        f.Numerator.Should().Be(1);
        f.Denominator.Should().Be(2);
    }

    // --- ToDouble ---

    [Fact]
    public void Fraction_ToDouble()
    {
        new Fraction(1, 3).ToDouble().Should().BeApproximately(1.0 / 3.0, 1e-15);
    }
}
