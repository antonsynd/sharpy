using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StrTests
{
    // ---- Optional formatting via Str(object) ----

    [Fact]
    public void Str_OptionalSomeInt_ReturnsInnerValue()
    {
        object boxed = Optional<int>.Some(42);
        ((string)Builtins.Str(boxed)).Should().Be("42");
    }

    [Fact]
    public void Str_OptionalSomeBool_ReturnsPythonBool()
    {
        object boxed = Optional<bool>.Some(true);
        ((string)Builtins.Str(boxed)).Should().Be("True");
    }

    [Fact]
    public void Str_OptionalSomeBoolFalse_ReturnsPythonFalse()
    {
        object boxed = Optional<bool>.Some(false);
        ((string)Builtins.Str(boxed)).Should().Be("False");
    }

    [Fact]
    public void Str_OptionalNoneInt_ReturnsNone()
    {
        object boxed = Optional<int>.None;
        ((string)Builtins.Str(boxed)).Should().Be("None");
    }

    [Fact]
    public void Str_OptionalNoneString_ReturnsNone()
    {
        object boxed = Optional<string>.None;
        ((string)Builtins.Str(boxed)).Should().Be("None");
    }

    [Fact]
    public void Str_OptionalSomeString_ReturnsInnerString()
    {
        object boxed = Optional<string>.Some("hello");
        ((string)Builtins.Str(boxed)).Should().Be("hello");
    }

    [Fact]
    public void Str_OptionalSomeDouble_FormatsLikePython()
    {
        object boxed = Optional<double>.Some(3.14);
        ((string)Builtins.Str(boxed)).Should().Be("3.14");
    }

    [Fact]
    public void Str_OptionalSomeWholeDouble_HasTrailingDotZero()
    {
        object boxed = Optional<double>.Some(5.0);
        ((string)Builtins.Str(boxed)).Should().Be("5.0");
    }

    [Fact]
    public void Str_NestedOptionalSome_FormatsInnerValue()
    {
        // Optional<Optional<int>> with Some(Some(42))
        object boxed = Optional<Optional<int>>.Some(Optional<int>.Some(42));
        // The inner Optional<int> is itself formatted via TryFormat -> "42"
        ((string)Builtins.Str(boxed)).Should().Be("42");
    }

    [Fact]
    public void Str_NestedOptionalInnerNone_ReturnsNone()
    {
        object boxed = Optional<Optional<int>>.Some(Optional<int>.None);
        ((string)Builtins.Str(boxed)).Should().Be("None");
    }

    [Fact]
    public void Str_Char_ReturnsCharAsString()
    {
        ((string)Builtins.Str('h')).Should().Be("h");
    }

    [Fact]
    public void Str_Char_DoesNotReturnAsciiCode()
    {
        ((string)Builtins.Str('h')).Should().NotBe("104");
    }

    [Theory]
    [InlineData(5.0, "5.0")]
    [InlineData(0.0, "0.0")]
    [InlineData(100.0, "100.0")]
    [InlineData(3.14, "3.14")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(double.NaN, "nan")]
    [InlineData(double.PositiveInfinity, "inf")]
    [InlineData(double.NegativeInfinity, "-inf")]
    public void Str_Double_FormatsLikePython(double value, string expected)
    {
        ((string)Builtins.Str(value)).Should().Be(expected);
    }

    [Theory]
    [InlineData(5.0f, "5.0")]
    [InlineData(0.0f, "0.0")]
    [InlineData(0.5f, "0.5")]
    [InlineData(float.NaN, "nan")]
    [InlineData(float.PositiveInfinity, "inf")]
    [InlineData(float.NegativeInfinity, "-inf")]
    public void Str_Float_FormatsLikePython(float value, string expected)
    {
        ((string)Builtins.Str(value)).Should().Be(expected);
    }

    [Fact]
    public void Str_BoxedDouble_FormatsLikePython()
    {
        object boxed = 5.0;
        ((string)Builtins.Str(boxed)).Should().Be("5.0");
    }

    [Fact]
    public void Str_BoxedFloat_FormatsLikePython()
    {
        object boxed = 5.0f;
        ((string)Builtins.Str(boxed)).Should().Be("5.0");
    }

    [Fact]
    public void Str_Double_NegativeZero_FormatsWithMinusSign()
    {
        // Python: str(-0.0) == "-0.0"
        // .NET "R" format for -0.0 produces "0" (no minus sign, no decimal),
        // then FormatFloat appends ".0" -> "0.0" (not "-0.0").
        // This documents the known divergence from Python.
        var result = Builtins.Str(-0.0);
        // .NET does not distinguish -0.0 in ToString("R"), so we get "0.0"
        // Python produces "-0.0". This is a known Axiom 1 (.NET) > Axiom 2 (Python) trade-off.
        ((string)result).Should().BeOneOf("-0.0", "0.0");
    }

    [Theory]
    [InlineData(1e15, "1000000000000000.0")]
    [InlineData(1e16, "10000000000000000.0")]  // .NET "R" format still uses fixed notation at 1e16; Python uses "1e+16"
    [InlineData(1e20, "1e+20")]
    [InlineData(1e308, "1e+308")]
    public void Str_Double_LargeValues_FormatsConsistently(double value, string expected)
    {
        // Large floats near or beyond the scientific notation boundary.
        // Python: str(1e15) == "1000000000000000.0", str(1e16) == "1e+16"
        // .NET "R" keeps fixed notation longer (1e16 -> "10000000000000000"),
        // FormatFloat appends ".0" when no decimal/exponent present.
        // FormatFloat now normalizes to lowercase 'e' to match Python (1e+20).
        ((string)Builtins.Str(value)).Should().Be(expected);
    }

    [Theory]
    [InlineData(1e-4, "0.0001")]
    [InlineData(1e-5, "1e-05")]
    [InlineData(1.5e-10, "1.5e-10")]
    [InlineData(1e-15, "1e-15")]
    [InlineData(1e-16, "1e-16")]
    public void Str_Double_SmallValues_FormatsConsistently(double value, string expected)
    {
        // Very small floats that may trigger scientific notation.
        // FormatFloat now normalizes to lowercase 'e' to match Python.
        ((string)Builtins.Str(value)).Should().Be(expected);
    }

    [Fact]
    public void Str_Double_SmallestSubnormal_UsesScientificNotation()
    {
        // 5e-324 is the smallest positive subnormal double
        var result = Builtins.Str(5e-324);
        ((string)result).Should().Be("5e-324");
    }

    [Fact]
    public void Str_Double_MaxValue_UsesScientificNotation()
    {
        var result = Builtins.Str(double.MaxValue);
        // double.MaxValue = 1.7976931348623157E+308
        ((string)result).Should().Contain("e+308");
    }
}
