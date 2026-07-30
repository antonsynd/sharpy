using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Repr_Tests
{
    [Theory]
    [InlineData("\0", "'\\x00'")]
    [InlineData("\a", "'\\x07'")]
    [InlineData("\b", "'\\x08'")]
    [InlineData("\f", "'\\x0c'")]
    [InlineData("\v", "'\\x0b'")]
    [InlineData("\x1b", "'\\x1b'")]
    [InlineData("\x1f", "'\\x1f'")]
    [InlineData("\x7f", "'\\x7f'")]
    public void Repr_ControlCharacter_EscapedAsHex(string input, string expected)
    {
        Builtins.Repr(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("\t", "'\\t'")]
    [InlineData("\n", "'\\n'")]
    [InlineData("\r", "'\\r'")]
    public void Repr_NamedEscapes_Preserved(string input, string expected)
    {
        Builtins.Repr(input).Should().Be(expected);
    }

    [Fact]
    public void Repr_MixedControlAndText_EscapesControlOnly()
    {
        Builtins.Repr("hello\x00world").Should().Be("'hello\\x00world'");
    }

    [Fact]
    public void Repr_ControlCharWithSmartQuoting_UsesDoubleQuotes()
    {
        // String contains single quote but no double quote → smart quoting picks double quotes
        Builtins.Repr("it's \a").Should().Be("\"it's \\x07\"");
    }

    [Fact]
    public void Repr_SingleElementTuple_HasTrailingComma()
    {
        // Python: repr((1,)) == "(1,)" — trailing comma disambiguates from grouping
        Builtins.Repr(System.ValueTuple.Create(1)).Should().Be("(1,)");
    }

    [Fact]
    public void Repr_TwoElementTuple_NoTrailingComma()
    {
        // Python: repr((1, 2)) == "(1, 2)"
        Builtins.Repr((1, 2)).Should().Be("(1, 2)");
    }

    [Fact]
    public void Repr_NestedSingleElementTuple_HasTrailingCommas()
    {
        // Python: repr(((1,),)) == "((1,),)"
        Builtins.Repr(System.ValueTuple.Create(System.ValueTuple.Create(1)))
            .Should().Be("((1,),)");
    }

    [Fact]
    public void Repr_SingleElementStringTuple_QuotesElement()
    {
        // Python: repr(("a",)) == "('a',)"
        Builtins.Repr(System.ValueTuple.Create("a")).Should().Be("('a',)");
    }

    [Fact]
    public void Repr_EightElementTuple_FlattensRest()
    {
        // ValueTuple packs the 8th element into a nested TRest; repr must flatten.
        // Python: repr((1,2,3,4,5,6,7,8)) == "(1, 2, 3, 4, 5, 6, 7, 8)"
        Builtins.Repr((1, 2, 3, 4, 5, 6, 7, 8))
            .Should().Be("(1, 2, 3, 4, 5, 6, 7, 8)");
    }

    [Fact]
    public void Repr_FifteenElementTuple_FlattensNestedRest()
    {
        // 15 elements span two levels of TRest nesting.
        Builtins.Repr((1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15))
            .Should().Be("(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15)");
    }

    [Theory]
    [InlineData(-4.0, "-4.0")]
    [InlineData(0.0, "0.0")]
    [InlineData(0.5, "0.5")]
    [InlineData(1e300, "1e+300")]
    [InlineData(double.NaN, "nan")]
    [InlineData(double.PositiveInfinity, "inf")]
    [InlineData(double.NegativeInfinity, "-inf")]
    public void Repr_Double_UsesPythonFloatFormat(double value, string expected)
    {
        // Python: repr(-4.0) == '-4.0', repr(1e300) == '1e+300'
        Builtins.Repr(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(-4.0f, "-4.0")]
    [InlineData(0.5f, "0.5")]
    [InlineData(float.NaN, "nan")]
    [InlineData(float.PositiveInfinity, "inf")]
    public void Repr_Float32_UsesPythonFloatFormat(float value, string expected)
    {
        Builtins.Repr(value).Should().Be(expected);
    }

    [Fact]
    public void Repr_ListOfWholeFloats_KeepsTrailingZero()
    {
        // Python: print([-4.0, 0.5]) == '[-4.0, 0.5]'
        new List<double>(new[] { -4.0, 0.5 }).ToString().Should().Be("[-4.0, 0.5]");
    }

    [Fact]
    public void Repr_TupleOfWholeFloats_KeepsTrailingZero()
    {
        // Python: print((-4.0, 0.5)) == '(-4.0, 0.5)'
        Builtins.Repr((-4.0, 0.5)).Should().Be("(-4.0, 0.5)");
    }

    [Fact]
    public void Repr_SetOfWholeFloat_KeepsTrailingZero()
    {
        // Single element keeps the assertion independent of set iteration order.
        new Set<double>(new[] { 1.0 }).ToString().Should().Be("{1.0}");
    }

    [Fact]
    public void Repr_DictWithFloatKeyAndValue_KeepsTrailingZero()
    {
        // Python: print({'a': -4.0}) == "{'a': -4.0}"
        var byName = new Dict<string, double>();
        byName["a"] = -4.0;
        byName.ToString().Should().Be("{'a': -4.0}");

        var byFloat = new Dict<double, double>();
        byFloat[2.0] = 3.0;
        byFloat.ToString().Should().Be("{2.0: 3.0}");
    }

    [Fact]
    public void Repr_NestedListOfFloats_KeepsTrailingZero()
    {
        // Python: print([[1.0, 2.0], [3.0]]) == '[[1.0, 2.0], [3.0]]'
        var outer = new List<List<double>>();
        outer.Append(new List<double>(new[] { 1.0, 2.0 }));
        outer.Append(new List<double>(new[] { 3.0 }));
        outer.ToString().Should().Be("[[1.0, 2.0], [3.0]]");
    }

    [Fact]
    public void Repr_ListWithNanAndInf_UsesPythonNames()
    {
        // Python: print([nan, inf, -inf]) == '[nan, inf, -inf]'
        new List<double>(new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            .ToString().Should().Be("[nan, inf, -inf]");
    }

    [Fact]
    public void Repr_ListWithScientificNotation_UsesLowercaseE()
    {
        // Python: print([1e300, 1e-07]) == '[1e+300, 1e-07]'
        new List<double>(new[] { 1e300 }).ToString().Should().Be("[1e+300]");
    }

    [Fact]
    public void Repr_ListOfFloat32_KeepsTrailingZero()
    {
        new List<float>(new[] { -4.0f, 0.5f }).ToString().Should().Be("[-4.0, 0.5]");
    }
}
