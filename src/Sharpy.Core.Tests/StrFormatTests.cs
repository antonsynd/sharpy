using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StrFormatTests
{
    #region Positional Arguments

    [Fact]
    public void Format_ExplicitPositional_ReplacesCorrectly()
    {
        var s = new Str("{0} {1}");
        ((string)s.Format("a", "b")).Should().Be("a b");
    }

    [Fact]
    public void Format_AutoNumbering_ReplacesInOrder()
    {
        var s = new Str("{} {}");
        ((string)s.Format("a", "b")).Should().Be("a b");
    }

    [Fact]
    public void Format_ThreeAutoArgs_ReplacesAll()
    {
        var s = new Str("{} {} {}");
        ((string)s.Format(1, 2, 3)).Should().Be("1 2 3");
    }

    [Fact]
    public void Format_RepeatedPositional_AllowsReuse()
    {
        var s = new Str("{0} {1} {0}");
        ((string)s.Format("a", "b")).Should().Be("a b a");
    }

    [Fact]
    public void Format_MixedAutoAndManual_ThrowsValueError()
    {
        var s = new Str("{} {0}");
        var act = () => s.Format(1, 2);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Format_MixedManualAndAuto_ThrowsValueError()
    {
        var s = new Str("{0} {}");
        var act = () => s.Format(1, 2);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Format_IndexOutOfRange_ThrowsIndexError()
    {
        var s = new Str("{2}");
        var act = () => s.Format("only_one");
        act.Should().Throw<IndexError>();
    }

    #endregion

    #region Literal Braces

    [Fact]
    public void Format_LiteralBraces_Escaped()
    {
        var s = new Str("{{}}");
        ((string)s.Format()).Should().Be("{}");
    }

    [Fact]
    public void Format_LiteralBraceWithPositional_Mixed()
    {
        var s = new Str("{{0}}");
        ((string)s.Format()).Should().Be("{0}");
    }

    #endregion

    #region Format Specifiers

    [Fact]
    public void Format_FloatPrecision()
    {
        var s = new Str("{:.2f}");
        ((string)s.Format(3.14159)).Should().Be("3.14");
    }

    [Fact]
    public void Format_RightAlign()
    {
        var s = new Str("{:>10}");
        ((string)s.Format("hello")).Should().Be("     hello");
    }

    [Fact]
    public void Format_LeftAlign()
    {
        var s = new Str("{:<10}");
        ((string)s.Format("hello")).Should().Be("hello     ");
    }

    [Fact]
    public void Format_CenterAlign()
    {
        var s = new Str("{:^10}");
        ((string)s.Format("hello")).Should().Be("  hello   ");
    }

    [Fact]
    public void Format_CenterAlignOddPadding()
    {
        var s = new Str("{:^9}");
        ((string)s.Format("hello")).Should().Be("  hello  ");
    }

    [Fact]
    public void Format_ZeroPadded()
    {
        var s = new Str("{:05d}");
        ((string)s.Format(42)).Should().Be("00042");
    }

    [Fact]
    public void Format_ForceSign()
    {
        var s = new Str("{:+d}");
        ((string)s.Format(42)).Should().Be("+42");
    }

    [Fact]
    public void Format_ForceSignNegative()
    {
        var s = new Str("{:+d}");
        ((string)s.Format(-42)).Should().Be("-42");
    }

    [Fact]
    public void Format_SpaceSign()
    {
        var s = new Str("{: d}");
        ((string)s.Format(42)).Should().Be(" 42");
    }

    [Fact]
    public void Format_ThousandsSeparator()
    {
        var s = new Str("{:,}");
        ((string)s.Format(1234567)).Should().Be("1,234,567");
    }

    [Fact]
    public void Format_Hex()
    {
        var s = new Str("{:x}");
        ((string)s.Format(255)).Should().Be("ff");
    }

    [Fact]
    public void Format_HexUpper()
    {
        var s = new Str("{:X}");
        ((string)s.Format(255)).Should().Be("FF");
    }

    [Fact]
    public void Format_Octal()
    {
        var s = new Str("{:o}");
        ((string)s.Format(255)).Should().Be("377");
    }

    [Fact]
    public void Format_Binary()
    {
        var s = new Str("{:b}");
        ((string)s.Format(255)).Should().Be("11111111");
    }

    [Fact]
    public void Format_Scientific()
    {
        var s = new Str("{:e}");
        ((string)s.Format(3.14)).Should().Be("3.140000e+00");
    }

    [Fact]
    public void Format_ScientificUpper()
    {
        var s = new Str("{:E}");
        ((string)s.Format(3.14)).Should().Be("3.140000E+00");
    }

    [Fact]
    public void Format_Percentage()
    {
        var s = new Str("{:%}");
        ((string)s.Format(0.85)).Should().Be("85.000000%");
    }

    [Fact]
    public void Format_PercentagePrecision()
    {
        var s = new Str("{:.1%}");
        ((string)s.Format(0.856)).Should().Be("85.6%");
    }

    [Fact]
    public void Format_FillCharRightAlign()
    {
        var s = new Str("{:*>10}");
        ((string)s.Format("hello")).Should().Be("*****hello");
    }

    [Fact]
    public void Format_FillCharLeftAlign()
    {
        var s = new Str("{:*<10}");
        ((string)s.Format("hello")).Should().Be("hello*****");
    }

    [Fact]
    public void Format_FillCharCenterAlign()
    {
        var s = new Str("{:*^10}");
        ((string)s.Format("hello")).Should().Be("**hello***");
    }

    [Fact]
    public void Format_AltFormBinary()
    {
        var s = new Str("{:#b}");
        ((string)s.Format(10)).Should().Be("0b1010");
    }

    [Fact]
    public void Format_AltFormOctal()
    {
        var s = new Str("{:#o}");
        ((string)s.Format(10)).Should().Be("0o12");
    }

    [Fact]
    public void Format_AltFormHex()
    {
        var s = new Str("{:#x}");
        ((string)s.Format(10)).Should().Be("0xa");
    }

    [Fact]
    public void Format_StringType()
    {
        var s = new Str("{:s}");
        ((string)s.Format("hello")).Should().Be("hello");
    }

    [Fact]
    public void Format_DefaultFloat()
    {
        var s = new Str("{:f}");
        ((string)s.Format(3.14)).Should().Be("3.140000");
    }

    [Fact]
    public void Format_PositionalWithFormatSpec()
    {
        var s = new Str("{0:.2f}");
        ((string)s.Format(3.14159)).Should().Be("3.14");
    }

    #endregion

    #region Formatmap

    [Fact]
    public void Formatmap_BasicKeywords()
    {
        var s = new Str("{name} is {age}");
        var mapping = new Dict<Str, object>();
        mapping[new Str("name")] = "Alice";
        mapping[new Str("age")] = 30;
        ((string)s.Formatmap(mapping)).Should().Be("Alice is 30");
    }

    [Fact]
    public void Formatmap_WithFormatSpec()
    {
        var s = new Str("{name:>10}");
        var mapping = new Dict<Str, object>();
        mapping[new Str("name")] = "Alice";
        ((string)s.Formatmap(mapping)).Should().Be("     Alice");
    }

    [Fact]
    public void Formatmap_MissingKey_ThrowsKeyError()
    {
        var s = new Str("{missing}");
        var mapping = new Dict<Str, object>();
        mapping[new Str("name")] = "Alice";
        var act = () => s.Formatmap(mapping);
        act.Should().Throw<KeyError>();
    }

    #endregion
}
