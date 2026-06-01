using System.Numerics;
using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

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
    public void Fraction_FromDouble_ExactRepresentation_Half()
    {
        var f = new Fraction(0.5);
        f.Numerator.Should().Be(1);
        f.Denominator.Should().Be(2);
    }

    [Fact]
    public void Fraction_FromDouble_ExactRepresentation_Quarter()
    {
        var f = new Fraction(0.25);
        f.Numerator.Should().Be(1);
        f.Denominator.Should().Be(4);
    }

    [Fact]
    public void Fraction_FromDouble_0_1_ExactBinaryRepresentation()
    {
        // Python: Fraction(0.1) == Fraction(3602879701896397, 36028797018963968)
        var f = new Fraction(0.1);
        f.Numerator.Should().Be(BigInteger.Parse("3602879701896397"));
        f.Denominator.Should().Be(BigInteger.Parse("36028797018963968"));
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
    public void Fraction_CopyConstructor()
    {
        var original = new Fraction(3, 7);
        var copy = new Fraction(original);
        copy.Numerator.Should().Be(3);
        copy.Denominator.Should().Be(7);
    }

    // --- String construction ---

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
    public void Fraction_FromString_ScientificNotation_NegativeExponent()
    {
        var f = new Fraction("1e-2");
        f.Numerator.Should().Be(1);
        f.Denominator.Should().Be(100);
    }

    [Fact]
    public void Fraction_FromString_ScientificNotation_PositiveExponent()
    {
        var f = new Fraction("1.5e2");
        f.Numerator.Should().Be(150);
        f.Denominator.Should().Be(1);
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
    public void Fraction_ModuloOperator()
    {
        var result = new Fraction(7, 2) % new Fraction(3, 2);
        result.Should().Be(new Fraction(1, 2));
    }

    // --- FloorDiv (returns long) ---

    [Fact]
    public void Fraction_FloorDiv_ReturnsLong()
    {
        long result = new Fraction(7, 2).FloorDiv(new Fraction(3, 2));
        result.Should().Be(2L);
    }

    [Fact]
    public void Fraction_FloorDiv_Negative_ReturnsLong()
    {
        long result = new Fraction(-7, 2).FloorDiv(new Fraction(3, 2));
        result.Should().Be(-3L);
    }

    [Fact]
    public void Fraction_FloorDiv_Exact()
    {
        long result = new Fraction(6, 1).FloorDiv(new Fraction(3, 1));
        result.Should().Be(2L);
    }

    [Fact]
    public void Fraction_FloorDiv_ByZero_Throws()
    {
        var act = () => new Fraction(1, 2).FloorDiv(new Fraction(0));
        act.Should().Throw<ZeroDivisionError>();
    }

    // --- Mod ---

    [Fact]
    public void Fraction_Mod()
    {
        var result = Fraction.Mod(new Fraction(7, 2), new Fraction(3, 2));
        result.Should().Be(new Fraction(1, 2));
    }

    [Fact]
    public void Fraction_Mod_ByZero_Throws()
    {
        var act = () => Fraction.Mod(new Fraction(1), new Fraction(0));
        act.Should().Throw<ZeroDivisionError>();
    }

    // --- Pow ---

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

    // --- Abs ---

    [Fact]
    public void Fraction_Abs_Negative()
    {
        var result = new Fraction(-3, 4).Abs();
        result.Numerator.Should().Be(3);
        result.Denominator.Should().Be(4);
    }

    [Fact]
    public void Fraction_Abs_Positive()
    {
        var result = new Fraction(3, 4).Abs();
        result.Numerator.Should().Be(3);
        result.Denominator.Should().Be(4);
    }

    [Fact]
    public void Fraction_Abs_Zero()
    {
        var result = new Fraction(0).Abs();
        result.Should().Be(new Fraction(0));
    }

    // --- ToLong ---

    [Fact]
    public void Fraction_ToLong_Positive()
    {
        // Python: int(Fraction(7,2)) == 3 (truncate toward zero)
        new Fraction(7, 2).ToLong().Should().Be(3L);
    }

    [Fact]
    public void Fraction_ToLong_Negative()
    {
        // Python: int(Fraction(-7,2)) == -3 (truncate toward zero)
        new Fraction(-7, 2).ToLong().Should().Be(-3L);
    }

    [Fact]
    public void Fraction_ToLong_Exact()
    {
        new Fraction(6, 3).ToLong().Should().Be(2L);
    }

    // --- Mixed arithmetic with long ---

    [Fact]
    public void Fraction_Add_Long()
    {
        var result = new Fraction(1, 2) + 3L;
        result.Should().Be(new Fraction(7, 2));
    }

    [Fact]
    public void Fraction_Long_Add()
    {
        var result = 3L + new Fraction(1, 2);
        result.Should().Be(new Fraction(7, 2));
    }

    [Fact]
    public void Fraction_Subtract_Long()
    {
        var result = new Fraction(7, 2) - 1L;
        result.Should().Be(new Fraction(5, 2));
    }

    [Fact]
    public void Fraction_Long_Subtract()
    {
        var result = 3L - new Fraction(1, 2);
        result.Should().Be(new Fraction(5, 2));
    }

    [Fact]
    public void Fraction_Multiply_Long()
    {
        var result = new Fraction(1, 3) * 6L;
        result.Should().Be(new Fraction(2));
    }

    [Fact]
    public void Fraction_Long_Multiply()
    {
        var result = 6L * new Fraction(1, 3);
        result.Should().Be(new Fraction(2));
    }

    [Fact]
    public void Fraction_Divide_Long()
    {
        var result = new Fraction(3, 2) / 3L;
        result.Should().Be(new Fraction(1, 2));
    }

    [Fact]
    public void Fraction_Long_Divide()
    {
        var result = 3L / new Fraction(2, 1);
        result.Should().Be(new Fraction(3, 2));
    }

    [Fact]
    public void Fraction_Mod_Long()
    {
        var result = new Fraction(7, 2) % 2L;
        result.Should().Be(new Fraction(3, 2));
    }

    [Fact]
    public void Fraction_Long_Mod()
    {
        // Python: 7 % Fraction(3, 2) == 1
        var result = 7L % new Fraction(3, 2);
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

    // --- LimitDenominator ---

    [Fact]
    public void Fraction_LimitDenominator_Pi()
    {
        var pi = new Fraction("3.141592653589793");
        var approx = pi.LimitDenominator(1000);
        approx.Numerator.Should().Be(355);
        approx.Denominator.Should().Be(113);
    }

    [Fact]
    public void Fraction_LimitDenominator_AlreadyBelow()
    {
        var f = new Fraction(1, 3);
        var result = f.LimitDenominator(10);
        result.Should().Be(f);
    }

    [Fact]
    public void Fraction_LimitDenominator_InvalidMax_Throws()
    {
        var f = new Fraction(1, 3);
        var act = () => f.LimitDenominator(0);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Fraction_LimitDenominator_355_113_MaxDenom100()
    {
        // Python: Fraction(355, 113).limit_denominator(100) == Fraction(311, 99)
        var f = new Fraction(355, 113);
        var result = f.LimitDenominator(100);
        result.Numerator.Should().Be(311);
        result.Denominator.Should().Be(99);
    }

    [Fact]
    public void Fraction_LimitDenominator_0_1_MaxDenom10()
    {
        // Python: Fraction(0.1).limit_denominator(10) == Fraction(1, 10)
        var f = new Fraction(0.1);
        var result = f.LimitDenominator(10);
        result.Numerator.Should().Be(1);
        result.Denominator.Should().Be(10);
    }

    // --- ToString ---

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
