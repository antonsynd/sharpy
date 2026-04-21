using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for Str, Bool, and Float conversion functions.
/// Int, Long, and Double are covered in their own dedicated test files.
/// </summary>
public class BuiltinConversion_Tests
{
    // ── Str ──

    [Fact]
    public void Str_Int_ReturnsStringRepresentation()
    {
        Str(42).Should().Be("42");
    }

    [Fact]
    public void Str_NegativeInt_ReturnsStringRepresentation()
    {
        Str(-7).Should().Be("-7");
    }

    [Fact]
    public void Str_Long_ReturnsStringRepresentation()
    {
        Str(100L).Should().Be("100");
    }

    [Fact]
    public void Str_Double_WholeNumber_AppendsDotZero()
    {
        Str(3.0).Should().Be("3.0");
    }

    [Fact]
    public void Str_Double_WithDecimal_ReturnsDecimalString()
    {
        Str(3.14).Should().Be("3.14");
    }

    [Fact]
    public void Str_Double_PositiveInfinity_ReturnsInf()
    {
        Str(double.PositiveInfinity).Should().Be("inf");
    }

    [Fact]
    public void Str_Double_NegativeInfinity_ReturnsNegativeInf()
    {
        Str(double.NegativeInfinity).Should().Be("-inf");
    }

    [Fact]
    public void Str_Double_NaN_ReturnsNan()
    {
        Str(double.NaN).Should().Be("nan");
    }

    [Fact]
    public void Str_Float_WholeNumber_AppendsDotZero()
    {
        Str(2.0f).Should().Be("2.0");
    }

    [Fact]
    public void Str_Float_PositiveInfinity_ReturnsInf()
    {
        Str(float.PositiveInfinity).Should().Be("inf");
    }

    [Fact]
    public void Str_Bool_True_ReturnsPythonTrue()
    {
        Str(true).Should().Be("True");
    }

    [Fact]
    public void Str_Bool_False_ReturnsPythonFalse()
    {
        Str(false).Should().Be("False");
    }

    [Fact]
    public void Str_Null_ReturnsNone()
    {
        Str((object)null!).Should().Be("None");
    }

    [Fact]
    public void Str_StringPassthrough_ReturnsSameString()
    {
        Str("hello").Should().Be("hello");
    }

    [Fact]
    public void Str_Char_ReturnsSingleCharString()
    {
        Str('A').Should().Be("A");
    }

    // ── Bool ──

    [Fact]
    public void Bool_Zero_ReturnsFalse()
    {
        Bool(0).Should().BeFalse();
    }

    [Fact]
    public void Bool_One_ReturnsTrue()
    {
        Bool(1).Should().BeTrue();
    }

    [Fact]
    public void Bool_NegativeInt_ReturnsTrue()
    {
        Bool(-1).Should().BeTrue();
    }

    [Fact]
    public void Bool_EmptyString_ReturnsFalse()
    {
        Bool("").Should().BeFalse();
    }

    [Fact]
    public void Bool_NonEmptyString_ReturnsTrue()
    {
        Bool("a").Should().BeTrue();
    }

    [Fact]
    public void Bool_ZeroDouble_ReturnsFalse()
    {
        Bool(0.0).Should().BeFalse();
    }

    [Fact]
    public void Bool_NonZeroDouble_ReturnsTrue()
    {
        Bool(3.14).Should().BeTrue();
    }

    [Fact]
    public void Bool_ZeroFloat_ReturnsFalse()
    {
        Bool(0.0f).Should().BeFalse();
    }

    [Fact]
    public void Bool_ZeroLong_ReturnsFalse()
    {
        Bool(0L).Should().BeFalse();
    }

    [Fact]
    public void Bool_NonZeroLong_ReturnsTrue()
    {
        Bool(100L).Should().BeTrue();
    }

    [Fact]
    public void Bool_Null_ReturnsFalse()
    {
        Bool((object?)null).Should().BeFalse();
    }

    [Fact]
    public void Bool_EmptyList_ReturnsFalse()
    {
        // Python: bool([]) == False
        var empty = new List<int>();
        Bool(empty).Should().BeFalse();
    }

    [Fact]
    public void Bool_NonEmptyList_ReturnsTrue()
    {
        // Python: bool([1]) == True
        var list = new List<int> { 1 };
        Bool(list).Should().BeTrue();
    }

    [Fact]
    public void Bool_ISized_EmptyCount_ReturnsFalse()
    {
        // ISized with Count == 0 should be falsy
        var sizedEmpty = new FakeSized(0);
        Bool(sizedEmpty).Should().BeFalse();
    }

    [Fact]
    public void Bool_ISized_NonEmptyCount_ReturnsTrue()
    {
        var sizedNonEmpty = new FakeSized(5);
        Bool(sizedNonEmpty).Should().BeTrue();
    }

    [Fact]
    public void Bool_IBoolConvertible_IsTrue_ReturnsTrue()
    {
        var truthy = new FakeBoolConvertible(true);
        Bool(truthy).Should().BeTrue();
    }

    [Fact]
    public void Bool_IBoolConvertible_IsFalse_ReturnsFalse()
    {
        var falsy = new FakeBoolConvertible(false);
        Bool(falsy).Should().BeFalse();
    }

    // ── Float ──

    [Fact]
    public void Float_FromInt_ReturnsDouble()
    {
        Float(42).Should().Be(42.0);
    }

    [Fact]
    public void Float_FromLong_ReturnsDouble()
    {
        Float(100L).Should().Be(100.0);
    }

    [Fact]
    public void Float_FromBoolTrue_ReturnsOne()
    {
        Float(true).Should().Be(1.0);
    }

    [Fact]
    public void Float_FromBoolFalse_ReturnsZero()
    {
        Float(false).Should().Be(0.0);
    }

    [Fact]
    public void Float_FromDouble_ReturnsIdentity()
    {
        Float(3.14).Should().Be(3.14);
    }

    [Fact]
    public void Float_FromDecimal_ReturnsDouble()
    {
        Float(2.5m).Should().Be(2.5);
    }

    [Fact]
    public void Float_FromString_ValidNumber_ReturnsDouble()
    {
        Float("3.14").Should().Be(3.14);
    }

    [Fact]
    public void Float_FromString_IntegerString_ReturnsDouble()
    {
        Float("42").Should().Be(42.0);
    }

    [Fact]
    public void Float_FromString_Empty_ThrowsValueError()
    {
        FluentActions.Invoking(() => Float(""))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Float_FromString_Invalid_ThrowsValueError()
    {
        FluentActions.Invoking(() => Float("not_a_number"))
            .Should().Throw<ValueError>();
    }

    // ── Helpers ──

    private sealed class FakeSized : ISized
    {
        public FakeSized(int count) => Count = count;
        public int Count { get; }
    }

    private sealed class FakeBoolConvertible : IBoolConvertible
    {
        public FakeBoolConvertible(bool value) => IsTrue = value;
        public bool IsTrue { get; }
    }
}
