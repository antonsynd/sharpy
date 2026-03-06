using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for additional <see cref="StringExtensions"/> methods:
/// split, rsplit, replace, startswith, endswith, index, rindex,
/// partition, rpartition, expandtabs, istitle, encode, maketrans, translate.
/// </summary>
public class StringExtensionsAdditionalTests
{
    // ================================================================
    // split()
    // ================================================================

    #region Split

    [Fact]
    public void Split_NoArgs_SplitsOnWhitespace()
    {
        "a  b  c".Split().Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Split_NoArgs_CollapsesConsecutiveWhitespace()
    {
        "  hello   world  ".Split().Should().Equal("hello", "world");
    }

    [Fact]
    public void Split_NoArgs_EmptyString_ReturnsEmptyList()
    {
        "".Split().Should().BeEmpty();
    }

    [Fact]
    public void Split_NoArgs_AllWhitespace_ReturnsEmptyList()
    {
        "   ".Split().Should().BeEmpty();
    }

    [Fact]
    public void Split_WithSep_SplitsOnSeparator()
    {
        "a,b,c".Split(",").Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Split_WithSep_ConsecutiveSeparators_PreservesEmptyStrings()
    {
        "a,,b".Split(",").Should().Equal("a", "", "b");
    }

    [Fact]
    public void Split_WithSep_EmptyString_ReturnsListWithEmptyString()
    {
        "".Split(",").Should().Equal("");
    }

    [Fact]
    public void Split_WithSep_SepOnly_ReturnsTwoEmptyStrings()
    {
        ",".Split(",").Should().Equal("", "");
    }

    [Fact]
    public void Split_EmptySep_ThrowsValueError()
    {
        var act = () => "hello".Split("");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Split_WithMaxsplit_LimitsNumberOfSplits()
    {
        // Python: "a,b,c".split(",", 1) → ['a', 'b,c']
        "a,b,c".Split(",", 1).Should().Equal("a", "b,c");
    }

    [Fact]
    public void Split_MaxsplitZero_ReturnsWholeString()
    {
        // Python: "a,b,c".split(",", 0) → ['a,b,c']
        "a,b,c".Split(",", 0).Should().Equal("a,b,c");
    }

    [Fact]
    public void Split_MaxsplitLargerThanOccurrences_SplitsAll()
    {
        "a".Split(",", 5).Should().Equal("a");
    }

    [Fact]
    public void Split_NegativeMaxsplit_SplitsAll()
    {
        "a,b,c".Split(",", -1).Should().Equal("a", "b", "c");
    }

    #endregion

    // ================================================================
    // rsplit()
    // ================================================================

    #region Rsplit

    [Fact]
    public void Rsplit_NoArgs_SameAsSplit()
    {
        "a  b  c".Rsplit().Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Rsplit_WithSep_NoMaxsplit_SameAsSplit()
    {
        "a,b,c".Rsplit(",").Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Rsplit_WithMaxsplit_SplitsFromRight()
    {
        // Python: "a,b,c".rsplit(",", 1) → ['a,b', 'c']
        "a,b,c".Rsplit(",", 1).Should().Equal("a,b", "c");
    }

    [Fact]
    public void Rsplit_WithMaxsplit_MultipleChars()
    {
        // Python: "a,b,c,d".rsplit(",", 2) → ['a,b', 'c', 'd']
        "a,b,c,d".Rsplit(",", 2).Should().Equal("a,b", "c", "d");
    }

    [Fact]
    public void Rsplit_MaxsplitZero_ReturnsWholeString()
    {
        "a,b,c,d".Rsplit(",", 0).Should().Equal("a,b,c,d");
    }

    #endregion

    // ================================================================
    // replace()
    // ================================================================

    #region Replace

    [Fact]
    public void Replace_AllOccurrences()
    {
        "hello".Replace("l", "r").Should().Be("herro");
    }

    [Fact]
    public void Replace_WithCount_LimitsReplacements()
    {
        // Python: "aaa".replace("a", "b", 2) → "bba"
        "aaa".Replace("a", "b", 2).Should().Be("bba");
    }

    [Fact]
    public void Replace_CountZero_NoChange()
    {
        "aaa".Replace("a", "b", 0).Should().Be("aaa");
    }

    [Fact]
    public void Replace_EmptyOld_InsertsEverywhere()
    {
        // Python: "aaa".replace("", "x") → "xaxaxax"
        "aaa".Replace("", "x").Should().Be("xaxaxax");
    }

    [Fact]
    public void Replace_EmptyOld_EmptyString()
    {
        // Python: "".replace("", "x") → "x"
        "".Replace("", "x").Should().Be("x");
    }

    [Fact]
    public void Replace_EmptyOld_WithCount()
    {
        // Python: "abc".replace("", "x", 2) → "xaxbc"
        "abc".Replace("", "x", 2).Should().Be("xaxbc");
    }

    [Fact]
    public void Replace_NotFound_NoChange()
    {
        "hello".Replace("xyz", "abc").Should().Be("hello");
    }

    [Fact]
    public void Replace_RemoveOccurrences()
    {
        "hello".Replace("l", "", 1).Should().Be("helo");
    }

    #endregion

    // ================================================================
    // startswith() / endswith()
    // ================================================================

    #region Startswith / Endswith

    [Fact]
    public void Startswith_BasicMatch()
    {
        "hello".Startswith("hel").Should().BeTrue();
    }

    [Fact]
    public void Startswith_NoMatch()
    {
        "hello".Startswith("world").Should().BeFalse();
    }

    [Fact]
    public void Startswith_WithStart()
    {
        // Python: "hello".startswith("ell", 1) → True
        "hello".Startswith("ell", 1).Should().BeTrue();
    }

    [Fact]
    public void Startswith_WithStartAndEnd()
    {
        "hello".Startswith("ell", 1, 4).Should().BeTrue();
    }

    [Fact]
    public void Startswith_NegativeStart()
    {
        // Python: "hello".startswith("llo", -3) → True
        "hello".Startswith("llo", -3).Should().BeTrue();
    }

    [Fact]
    public void Startswith_MultiplePrefixes()
    {
        "hello".Startswith("hel", "wor").Should().BeTrue();
        "hello".Startswith("foo", "bar").Should().BeFalse();
    }

    [Fact]
    public void Endswith_BasicMatch()
    {
        "hello".Endswith("llo").Should().BeTrue();
    }

    [Fact]
    public void Endswith_NoMatch()
    {
        "hello".Endswith("world").Should().BeFalse();
    }

    [Fact]
    public void Endswith_WithStartAndEnd()
    {
        // Python: "hello".endswith("ll", 0, 4) → True
        "hello".Endswith("ll", 0, 4).Should().BeTrue();
    }

    [Fact]
    public void Endswith_NegativeEnd()
    {
        // Python: "hello".endswith("hel", 0, -2) → True
        "hello".Endswith("hel", 0, -2).Should().BeTrue();
    }

    [Fact]
    public void Endswith_MultipleSuffixes()
    {
        "hello".Endswith("llo", "xyz").Should().BeTrue();
        "hello".Endswith("foo", "bar").Should().BeFalse();
    }

    #endregion

    // ================================================================
    // index() / rindex()
    // ================================================================

    #region Index / Rindex

    [Fact]
    public void Index_Found_ReturnsIndex()
    {
        "hello".Index("ll").Should().Be(2);
    }

    [Fact]
    public void Index_NotFound_ThrowsValueError()
    {
        var act = () => "hello".Index("xyz");
        act.Should().Throw<ValueError>().WithMessage("substring not found");
    }

    [Fact]
    public void Index_WithStart()
    {
        "hello hello".Index("hello", 1).Should().Be(6);
    }

    [Fact]
    public void Index_WithStartAndEnd()
    {
        var act = () => "hello".Index("hello", 1, 5);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Rindex_Found_ReturnsLastIndex()
    {
        "hello".Rindex("l").Should().Be(3);
    }

    [Fact]
    public void Rindex_NotFound_ThrowsValueError()
    {
        var act = () => "hello".Rindex("xyz");
        act.Should().Throw<ValueError>();
    }

    #endregion

    // ================================================================
    // partition() / rpartition()
    // ================================================================

    #region Partition / Rpartition

    [Fact]
    public void Partition_Found()
    {
        "hello world".Partition(" ").Should().Be(("hello", " ", "world"));
    }

    [Fact]
    public void Partition_NotFound()
    {
        "hello".Partition(" ").Should().Be(("hello", "", ""));
    }

    [Fact]
    public void Partition_EmptySep_ThrowsValueError()
    {
        var act = () => "hello".Partition("");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Rpartition_Found()
    {
        "hello world foo".Rpartition(" ").Should().Be(("hello world", " ", "foo"));
    }

    [Fact]
    public void Rpartition_NotFound()
    {
        // Python: "hello".rpartition(" ") → ("", "", "hello")
        "hello".Rpartition(" ").Should().Be(("", "", "hello"));
    }

    [Fact]
    public void Rpartition_EmptySep_ThrowsValueError()
    {
        var act = () => "hello".Rpartition("");
        act.Should().Throw<ValueError>();
    }

    #endregion

    // ================================================================
    // expandtabs()
    // ================================================================

    #region Expandtabs

    [Fact]
    public void Expandtabs_Default()
    {
        // Python: "01\t012\t0123\t01234".expandtabs()
        // → "01      012     0123    01234"
        "01\t012\t0123\t01234".Expandtabs().Should().Be("01      012     0123    01234");
    }

    [Fact]
    public void Expandtabs_CustomSize()
    {
        // Python: "01\t012\t0123\t01234".expandtabs(4) → "01  012 0123    01234"
        "01\t012\t0123\t01234".Expandtabs(4).Should().Be("01  012 0123    01234");
    }

    [Fact]
    public void Expandtabs_SingleTab()
    {
        // Python: "\t".expandtabs() → "        " (8 spaces)
        "\t".Expandtabs().Should().Be("        ");
    }

    [Fact]
    public void Expandtabs_EmptyString()
    {
        "".Expandtabs().Should().Be("");
    }

    [Fact]
    public void Expandtabs_NoTabs()
    {
        "no tabs".Expandtabs().Should().Be("no tabs");
    }

    [Fact]
    public void Expandtabs_MultipleTabs()
    {
        // Python: "\t\t".expandtabs(4) → "        " (4 + 4)
        "\t\t".Expandtabs(4).Should().Be("        ");
    }

    [Fact]
    public void Expandtabs_ColumnTracking()
    {
        // Python: "a\tb\tc".expandtabs(4) → "a   b   c"
        "a\tb\tc".Expandtabs(4).Should().Be("a   b   c");
    }

    #endregion

    // ================================================================
    // istitle()
    // ================================================================

    #region Istitle

    [Fact]
    public void Istitle_TitlecasedString()
    {
        "Hello World".Istitle().Should().BeTrue();
    }

    [Fact]
    public void Istitle_NotTitlecased()
    {
        "Hello world".Istitle().Should().BeFalse();
    }

    [Fact]
    public void Istitle_AllUpper_ReturnsFalse()
    {
        "HELLO".Istitle().Should().BeFalse();
    }

    [Fact]
    public void Istitle_EmptyString_ReturnsFalse()
    {
        "".Istitle().Should().BeFalse();
    }

    [Fact]
    public void Istitle_SingleUpperChar()
    {
        "A".Istitle().Should().BeTrue();
    }

    [Fact]
    public void Istitle_DigitsFollowedByUpper()
    {
        // Python: "1A".istitle() → True (digit is uncased, A after uncased = ok)
        "1A".Istitle().Should().BeTrue();
    }

    [Fact]
    public void Istitle_TrailingSpace()
    {
        // Python: "Hello ".istitle() → True
        "Hello ".Istitle().Should().BeTrue();
    }

    [Fact]
    public void Istitle_OnlyDigits_ReturnsFalse()
    {
        // Python: "123".istitle() → False (no cased characters)
        "123".Istitle().Should().BeFalse();
    }

    #endregion

    // ================================================================
    // encode()
    // ================================================================

    #region Encode

    [Fact]
    public void Encode_Utf8_Default()
    {
        "hello".Encode().Should().Equal(
            (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o');
    }

    [Fact]
    public void Encode_Ascii()
    {
        "hello".Encode("ascii").Should().Equal(
            (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o');
    }

    [Fact]
    public void Encode_UnknownEncoding_ThrowsValueError()
    {
        var act = () => "hello".Encode("invalid-encoding");
        act.Should().Throw<ValueError>();
    }

    #endregion

    // ================================================================
    // maketrans() / translate()
    // ================================================================

    #region Maketrans / Translate

    [Fact]
    public void Maketrans_BasicMapping()
    {
        var table = StringExtensions.Maketrans("abc", "xyz");
        "abcdef".Translate(table).Should().Be("xyzdef");
    }

    [Fact]
    public void Maketrans_WithDeletion()
    {
        var table = StringExtensions.Maketrans("abc", "xyz", "def");
        "abcdef".Translate(table).Should().Be("xyz");
    }

    [Fact]
    public void Maketrans_UnequalLengths_ThrowsValueError()
    {
        var act = () => StringExtensions.Maketrans("ab", "x");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Translate_NoMappingForChar_PassesThrough()
    {
        var table = StringExtensions.Maketrans("a", "b");
        "axyz".Translate(table).Should().Be("bxyz");
    }

    [Fact]
    public void Translate_EmptyString()
    {
        var table = StringExtensions.Maketrans("a", "b");
        "".Translate(table).Should().Be("");
    }

    #endregion
}
