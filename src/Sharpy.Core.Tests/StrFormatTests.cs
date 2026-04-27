using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StrFormatTests
{
    #region Positional Arguments

    [Fact]
    public void Format_ExplicitPositional_ReplacesCorrectly()
    {
        string s = "{0} {1}";
        s.Format("a", "b").Should().Be("a b");
    }

    [Fact]
    public void Format_AutoNumbering_ReplacesInOrder()
    {
        string s = "{} {}";
        s.Format("a", "b").Should().Be("a b");
    }

    [Fact]
    public void Format_ThreeAutoArgs_ReplacesAll()
    {
        string s = "{} {} {}";
        s.Format(1, 2, 3).Should().Be("1 2 3");
    }

    [Fact]
    public void Format_RepeatedPositional_AllowsReuse()
    {
        string s = "{0} {1} {0}";
        s.Format("a", "b").Should().Be("a b a");
    }

    [Fact]
    public void Format_MixedAutoAndManual_ThrowsValueError()
    {
        string s = "{} {0}";
        var act = () => s.Format(1, 2);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Format_MixedManualAndAuto_ThrowsValueError()
    {
        string s = "{0} {}";
        var act = () => s.Format(1, 2);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Format_IndexOutOfRange_ThrowsIndexError()
    {
        string s = "{2}";
        var act = () => s.Format("only_one");
        act.Should().Throw<IndexError>();
    }

    #endregion

    #region Literal Braces

    [Fact]
    public void Format_LiteralBraces_Escaped()
    {
        string s = "{{}}";
        s.Format().Should().Be("{}");
    }

    [Fact]
    public void Format_LiteralBraceWithPositional_Mixed()
    {
        string s = "{{0}}";
        s.Format().Should().Be("{0}");
    }

    #endregion

    #region Format Specifiers

    [Fact]
    public void Format_FloatPrecision()
    {
        string s = "{:.2f}";
        s.Format(3.14159).Should().Be("3.14");
    }

    [Fact]
    public void Format_RightAlign()
    {
        string s = "{:>10}";
        s.Format("hello").Should().Be("     hello");
    }

    [Fact]
    public void Format_LeftAlign()
    {
        string s = "{:<10}";
        s.Format("hello").Should().Be("hello     ");
    }

    [Fact]
    public void Format_CenterAlign()
    {
        string s = "{:^10}";
        s.Format("hello").Should().Be("  hello   ");
    }

    [Fact]
    public void Format_CenterAlignOddPadding()
    {
        string s = "{:^9}";
        s.Format("hello").Should().Be("  hello  ");
    }

    [Fact]
    public void Format_ZeroPadded()
    {
        string s = "{:05d}";
        s.Format(42).Should().Be("00042");
    }

    [Fact]
    public void Format_ForceSign()
    {
        string s = "{:+d}";
        s.Format(42).Should().Be("+42");
    }

    [Fact]
    public void Format_ForceSignNegative()
    {
        string s = "{:+d}";
        s.Format(-42).Should().Be("-42");
    }

    [Fact]
    public void Format_SpaceSign()
    {
        string s = "{: d}";
        s.Format(42).Should().Be(" 42");
    }

    [Fact]
    public void Format_ThousandsSeparator()
    {
        string s = "{:,}";
        s.Format(1234567).Should().Be("1,234,567");
    }

    [Fact]
    public void Format_Hex()
    {
        string s = "{:x}";
        s.Format(255).Should().Be("ff");
    }

    [Fact]
    public void Format_HexUpper()
    {
        string s = "{:X}";
        s.Format(255).Should().Be("FF");
    }

    [Fact]
    public void Format_Octal()
    {
        string s = "{:o}";
        s.Format(255).Should().Be("377");
    }

    [Fact]
    public void Format_Binary()
    {
        string s = "{:b}";
        s.Format(255).Should().Be("11111111");
    }

    [Fact]
    public void Format_Scientific()
    {
        string s = "{:e}";
        s.Format(3.14).Should().Be("3.140000e+00");
    }

    [Fact]
    public void Format_ScientificUpper()
    {
        string s = "{:E}";
        s.Format(3.14).Should().Be("3.140000E+00");
    }

    [Fact]
    public void Format_Percentage()
    {
        string s = "{:%}";
        s.Format(0.85).Should().Be("85.000000%");
    }

    [Fact]
    public void Format_PercentagePrecision()
    {
        string s = "{:.1%}";
        s.Format(0.856).Should().Be("85.6%");
    }

    [Fact]
    public void Format_FillCharRightAlign()
    {
        string s = "{:*>10}";
        s.Format("hello").Should().Be("*****hello");
    }

    [Fact]
    public void Format_FillCharLeftAlign()
    {
        string s = "{:*<10}";
        s.Format("hello").Should().Be("hello*****");
    }

    [Fact]
    public void Format_FillCharCenterAlign()
    {
        string s = "{:*^10}";
        s.Format("hello").Should().Be("**hello***");
    }

    [Fact]
    public void Format_AltFormBinary()
    {
        string s = "{:#b}";
        s.Format(10).Should().Be("0b1010");
    }

    [Fact]
    public void Format_AltFormOctal()
    {
        string s = "{:#o}";
        s.Format(10).Should().Be("0o12");
    }

    [Fact]
    public void Format_AltFormHex()
    {
        string s = "{:#x}";
        s.Format(10).Should().Be("0xa");
    }

    [Fact]
    public void Format_StringType()
    {
        string s = "{:s}";
        s.Format("hello").Should().Be("hello");
    }

    [Fact]
    public void Format_DefaultFloat()
    {
        string s = "{:f}";
        s.Format(3.14).Should().Be("3.140000");
    }

    [Fact]
    public void Format_PositionalWithFormatSpec()
    {
        string s = "{0:.2f}";
        s.Format(3.14159).Should().Be("3.14");
    }

    #endregion

    #region FormatMap

    [Fact]
    public void FormatMap_BasicKeywords()
    {
        string s = "{name} is {age}";
        var mapping = new Dict<string, object>();
        mapping["name"] = "Alice";
        mapping["age"] = 30;
        s.FormatMap(mapping).Should().Be("Alice is 30");
    }

    [Fact]
    public void FormatMap_WithFormatSpec()
    {
        string s = "{name:>10}";
        var mapping = new Dict<string, object>();
        mapping["name"] = "Alice";
        s.FormatMap(mapping).Should().Be("     Alice");
    }

    [Fact]
    public void FormatMap_MissingKey_ThrowsKeyError()
    {
        string s = "{missing}";
        var mapping = new Dict<string, object>();
        mapping["name"] = "Alice";
        var act = () => s.FormatMap(mapping);
        act.Should().Throw<KeyError>();
    }

    #endregion

    #region Conversion Flags

    [Fact]
    public void Format_ConversionR_WrapsStringInQuotes()
    {
        string s = "{0!r}";
        s.Format("hello").Should().Be("'hello'");
    }

    [Fact]
    public void Format_ConversionS_CallsStr()
    {
        string s = "{0!s}";
        s.Format(42).Should().Be("42");
    }

    [Fact]
    public void Format_ConversionA_CallsAscii()
    {
        string s = "{0!a}";
        s.Format("hello").Should().Be("'hello'");
    }

    [Fact]
    public void Format_ConversionR_WithFormatSpec()
    {
        // '{0!r:.5}'.format('hello world') => "'hell"
        string s = "{0!r:.5}";
        s.Format("hello world").Should().Be("'hell");
    }

    [Fact]
    public void Format_ConversionR_None()
    {
        string s = "{0!r}";
        s.Format(new object[] { null! }).Should().Be("None");
    }

    [Fact]
    public void Format_ConversionS_None()
    {
        string s = "{0!s}";
        s.Format(new object[] { null! }).Should().Be("None");
    }

    [Fact]
    public void Format_ConversionR_AutoNumbering()
    {
        string s = "{!r}";
        s.Format("test").Should().Be("'test'");
    }

    [Fact]
    public void Format_ConversionS_AutoNumbering()
    {
        string s = "{!s}";
        s.Format(99).Should().Be("99");
    }

    [Fact]
    public void Format_UnknownConversion_ThrowsValueError()
    {
        string s = "{0!x}";
        var act = () => s.Format("hello");
        act.Should().Throw<ValueError>().WithMessage("*Unknown conversion specifier*");
    }

    #endregion

    #region Nested Field Access

    [Fact]
    public void Format_BracketIndex_ListAccess()
    {
        string s = "{0[0]}";
        s.Format(new List<int>(new[] { 10, 20 })).Should().Be("10");
    }

    [Fact]
    public void Format_BracketIndex_SecondElement()
    {
        string s = "{0[1]}";
        s.Format(new List<int>(new[] { 10, 20 })).Should().Be("20");
    }

    [Fact]
    public void Format_BracketKey_DictAccess()
    {
        string s = "{0[key]}";
        var d = new Dict<string, object>();
        d["key"] = "val";
        s.Format(d).Should().Be("val");
    }

    [Fact]
    public void Format_BracketIndex_OutOfRange_ThrowsIndexError()
    {
        string s = "{0[99]}";
        var act = () => s.Format(new List<int>(new[] { 10, 20 }));
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void Format_BracketKey_Missing_ThrowsKeyError()
    {
        string s = "{0[bad]}";
        var d = new Dict<string, object>();
        d["key"] = "val";
        var act = () => s.Format(d);
        act.Should().Throw<KeyError>();
    }

    [Fact]
    public void Format_DotAccess_Property()
    {
        // Access the Length property of a string
        string s = "{0.Length}";
        s.Format("hello").Should().Be("5");
    }

    [Fact]
    public void Format_DotAccess_MissingProperty_ThrowsAttributeError()
    {
        string s = "{0.nonexistent}";
        var act = () => s.Format("hello");
        act.Should().Throw<AttributeError>();
    }

    [Fact]
    public void Format_ChainedBracketAccess()
    {
        string s = "{0[0][1]}";
        var outer = new List<object>(new object[]
        {
            new List<int>(new[] { 10, 20 }),
            new List<int>(new[] { 30, 40 })
        });
        s.Format(outer).Should().Be("20");
    }

    [Fact]
    public void Format_BracketAccess_WithConversion()
    {
        string s = "{0[0]!r}";
        var list = new List<object>(new object[] { "hello" });
        s.Format(list).Should().Be("'hello'");
    }

    [Fact]
    public void Format_DotAccess_WithFormatSpec()
    {
        string s = "{0.Length:03d}";
        s.Format("hello").Should().Be("005");
    }

    #endregion
}
