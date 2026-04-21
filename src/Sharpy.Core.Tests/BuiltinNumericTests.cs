using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for builtin numeric functions: Abs, Round, Sum, Min, Max.
/// Specific edge cases NOT already covered in:
///   RoundTests.cs, PowTests.cs, DivModTests.cs, HexOctBinTests.cs,
///   SumStartTests.cs, MaxTests.cs, MinTests.cs, MinMaxDefaultTests.cs.
/// </summary>
public class BuiltinNumeric_Tests
{
    // ── Abs ──

    [Fact]
    public void Abs_NegativeInt_ReturnsPositive()
    {
        Abs(-5).Should().Be(5);
    }

    [Fact]
    public void Abs_ZeroInt_ReturnsZero()
    {
        Abs(0).Should().Be(0);
    }

    [Fact]
    public void Abs_PositiveInt_ReturnsSame()
    {
        Abs(5).Should().Be(5);
    }

    [Fact]
    public void Abs_NegativeLong_ReturnsPositive()
    {
        Abs(-100L).Should().Be(100L);
    }

    [Fact]
    public void Abs_NegativeDouble_ReturnsPositive()
    {
        Abs(-3.14).Should().BeApproximately(3.14, 0.0001);
    }

    [Fact]
    public void Abs_NegativeFloat_ReturnsPositive()
    {
        Abs(-2.5f).Should().BeApproximately(2.5f, 0.0001f);
    }

    [Fact]
    public void Abs_NegativeDecimal_ReturnsPositive()
    {
        Abs(-9.99m).Should().Be(9.99m);
    }

    [Fact]
    public void Abs_NegativeShort_ReturnsPositive()
    {
        short s = -100;
        Abs(s).Should().Be((short)100);
    }

    [Fact]
    public void Abs_NegativeSbyte_ReturnsPositive()
    {
        sbyte sb = -50;
        Abs(sb).Should().Be((sbyte)50);
    }

    [Fact]
    public void Abs_DoubleNaN_ReturnsNaN()
    {
        // Math.Abs(NaN) == NaN
        double result = Abs(double.NaN);
        double.IsNaN(result).Should().BeTrue();
    }

    // ── Round — cases not in RoundTests.cs ──

    [Fact]
    public void Round_DoubleHalfToEven_3_5_RoundsTo4()
    {
        // 3.5 rounds to 4 (nearest even is 4)
        // Python also uses banker's rounding
        Round(3.5).Should().Be(4);
    }

    [Fact]
    public void Round_DoubleNegative_3_5_RoundsTo_Negative4()
    {
        // -3.5 rounds to -4 (nearest even is -4)
        Round(-3.5).Should().Be(-4);
    }

    [Fact]
    public void Round_DoubleWith2Decimals_TruncatesAtPrecision()
    {
        // 3.146 rounded to 2 decimal places
        Round(3.146, 2).Should().BeApproximately(3.15, 0.001);
    }

    [Fact]
    public void Round_NegativePrecision_ThrowsArgumentOutOfRangeException()
    {
        // NOTE: Python supports round(x, -2) to round to nearest 100, but .NET's
        // Math.Round does not support negative digits. The Sharpy implementation
        // currently throws ArgumentOutOfRangeException for negative precision.
        FluentActions.Invoking(() => Round(1234.5, -2))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Round_ZeroDecimals_ReturnsWholeNumber()
    {
        Round(7.9).Should().Be(8);
    }

    // ── Sum — basic cases not in SumStartTests.cs ──

    [Fact]
    public void Sum_IntList_ReturnsTotal()
    {
        List<int> list = [1, 2, 3];
        Sum(list).Should().Be(6);
    }

    [Fact]
    public void Sum_EmptyIntList_ReturnsZero()
    {
        Sum(new List<int>()).Should().Be(0);
    }

    [Fact]
    public void Sum_DoubleList_ReturnsTotal()
    {
        List<double> list = [1.0, 2.0, 3.0];
        Sum(list).Should().BeApproximately(6.0, 0.0001);
    }

    [Fact]
    public void Sum_SingleElementList_ReturnsThatElement()
    {
        List<int> list = [42];
        Sum(list).Should().Be(42);
    }

    // ── Min — additional cases not in MinTests.cs / MinMaxDefaultTests.cs ──

    [Fact]
    public void Min_SingleElement_ReturnsThatElement()
    {
        List<int> list = [99];
        Min(list).Should().Be(99);
    }

    [Fact]
    public void Min_StringList_ReturnsLexicographicMin()
    {
        List<string> list = ["banana", "apple", "cherry"];
        Min(list).Should().Be("apple");
    }

    [Fact]
    public void Min_NullIterable_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Min<int>(null!))
            .Should().Throw<TypeError>();
    }

    // ── Max — additional cases not in MaxTests.cs / MinMaxDefaultTests.cs ──

    [Fact]
    public void Max_SingleElement_ReturnsThatElement()
    {
        List<int> list = [7];
        Max(list).Should().Be(7);
    }

    [Fact]
    public void Max_StringList_ReturnsLexicographicMax()
    {
        List<string> list = ["banana", "apple", "cherry"];
        Max(list).Should().Be("cherry");
    }

    [Fact]
    public void Max_NullIterable_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Max<int>(null!))
            .Should().Throw<TypeError>();
    }

    // ── DivMod — additional cases not in DivModTests.cs ──

    [Fact]
    public void Divmod_ZeroDividend_ReturnsZeroQuotientZeroRemainder()
    {
        var (q, r) = Divmod(0, 7);
        q.Should().Be(0);
        r.Should().Be(0);
    }

    [Fact]
    public void Divmod_BothNegative_ReturnsCorrectResult()
    {
        // Python: divmod(-10, -3) == (3, -1)
        // -10 // -3 = 3 (floor), -10 % -3 = -1
        var (q, r) = Divmod(-10, -3);
        q.Should().Be(3);
        r.Should().Be(-1);
    }

    [Fact]
    public void Divmod_NegativeDividend_PositiveDivisor_ReturnsPythonFlooredResult()
    {
        // Python: divmod(-10, 3) == (-4, 2)
        var (q, r) = Divmod(-10, 3);
        q.Should().Be(-4);
        r.Should().Be(2);
    }
}
