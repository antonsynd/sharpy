using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for additional string extension methods: split, rsplit, replace,
/// startswith, endswith, index, rindex, partition, rpartition, expandtabs,
/// istitle, encode, maketrans, and translate.
/// </summary>
/// <remarks>
/// Split and Replace extension methods are called as static methods because
/// C# instance methods on string (string.Split, string.Replace) take priority
/// over extension methods with the same name.
/// </remarks>
public class StringExtensionsAdditionalTests
{
    // ================================================================
    // Split
    // ================================================================

    #region Split

    [Fact]
    public void Split_NoArgs_SplitsOnWhitespace()
    {
        StringExtensions.Split("  hello  world  ").Should().Equal("hello", "world");
    }

    [Fact]
    public void Split_NoArgs_EmptyString_ReturnsEmpty()
    {
        StringExtensions.Split("").Should().BeEmpty();
    }

    [Fact]
    public void Split_NoArgs_WhitespaceOnly_ReturnsEmpty()
    {
        StringExtensions.Split("   ").Should().BeEmpty();
    }

    [Fact]
    public void Split_NoArgs_SingleWord()
    {
        StringExtensions.Split("hello").Should().Equal("hello");
    }

    [Fact]
    public void Split_NoArgs_TabsAndNewlines()
    {
        StringExtensions.Split("hello\t\nworld").Should().Equal("hello", "world");
    }

    [Fact]
    public void Split_WithSep_SplitsOnSeparator()
    {
        StringExtensions.Split("hello", "ll").Should().Equal("he", "o");
    }

    [Fact]
    public void Split_WithSep_Space()
    {
        // Python: '  hello  world  '.split(' ') -> ['', '', 'hello', '', 'world', '', '']
        StringExtensions.Split("  hello  world  ", " ").Should().Equal("", "", "hello", "", "world", "", "");
    }

    [Fact]
    public void Split_WithSep_Maxsplit()
    {
        StringExtensions.Split("hello world", " ", 1).Should().Equal("hello", "world");
    }

    [Fact]
    public void Split_WithSep_MaxsplitWithMultiCharSep()
    {
        StringExtensions.Split("aXbXcXd", "X", 2).Should().Equal("a", "b", "cXd");
    }

    [Fact]
    public void Split_WithSep_MaxsplitZero()
    {
        StringExtensions.Split("a b c", " ", 0).Should().Equal("a b c");
    }

    [Fact]
    public void Split_WithSep_NoOccurrence()
    {
        StringExtensions.Split("hello", "xyz").Should().Equal("hello");
    }

    [Fact]
    public void Split_EmptySep_ThrowsValueError()
    {
        var act = () => StringExtensions.Split("hello", "");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Split_WithSep_AtEdges()
    {
        StringExtensions.Split("XhelloX", "X").Should().Equal("", "hello", "");
    }

    #endregion

    // ================================================================
    // Rsplit
    // ================================================================

    #region Rsplit

    [Fact]
    public void Rsplit_NoArgs_SplitsOnWhitespace()
    {
        "  hello  world  ".Rsplit().Should().Equal("hello", "world");
    }

    [Fact]
    public void Rsplit_WithSep_Space()
    {
        "  hello  world  ".Rsplit(" ").Should().Equal("", "", "hello", "", "world", "", "");
    }

    [Fact]
    public void Rsplit_WithSep_Maxsplit()
    {
        // Python: 'hello world foo'.rsplit(' ', 1) -> ['hello world', 'foo']
        "hello world foo".Rsplit(" ", 1).Should().Equal("hello world", "foo");
    }

    [Fact]
    public void Rsplit_WithSep_MaxsplitMultiChar()
    {
        // Python: 'aXbXcXd'.rsplit('X', 2) -> ['aXb', 'c', 'd']
        "aXbXcXd".Rsplit("X", 2).Should().Equal("aXb", "c", "d");
    }

    [Fact]
    public void Rsplit_WithSep_MaxsplitZero()
    {
        "a b c".Rsplit(" ", 0).Should().Equal("a b c");
    }

    [Fact]
    public void Rsplit_EmptySep_ThrowsValueError()
    {
        var act = () => "hello".Rsplit("");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Rsplit_NoArgs_EmptyString()
    {
        "".Rsplit().Should().BeEmpty();
    }

    #endregion

    // ================================================================
    // Replace
    // ================================================================

    #region Replace

    [Fact]
    public void Replace_AllOccurrences()
    {
        StringExtensions.Replace("hello world hello", "hello", "hi").Should().Be("hi world hi");
    }

    [Fact]
    public void Replace_WithCount()
    {
        StringExtensions.Replace("hello world hello", "hello", "hi", 1).Should().Be("hi world hello");
    }

    [Fact]
    public void Replace_AllChars()
    {
        StringExtensions.Replace("aaa", "a", "bb").Should().Be("bbbbbb");
    }

    [Fact]
    public void Replace_WithCount_Partial()
    {
        StringExtensions.Replace("aaa", "a", "bb", 2).Should().Be("bbbba");
    }

    [Fact]
    public void Replace_EmptyOld_InsertsEverywhere()
    {
        // Python: 'hello'.replace('', '-') -> '-h-e-l-l-o-'
        StringExtensions.Replace("hello", "", "-").Should().Be("-h-e-l-l-o-");
    }

    [Fact]
    public void Replace_EmptyOld_EmptyString()
    {
        StringExtensions.Replace("", "", "-").Should().Be("-");
    }

    [Fact]
    public void Replace_NotFound()
    {
        StringExtensions.Replace("hello", "xyz", "abc").Should().Be("hello");
    }

    [Fact]
    public void Replace_WithCount_Zero()
    {
        StringExtensions.Replace("hello", "l", "L", 0).Should().Be("hello");
    }

    [Fact]
    public void Replace_WithCount_Negative()
    {
        // Negative count means replace all (like no count)
        StringExtensions.Replace("aaa", "a", "b", -1).Should().Be("bbb");
    }

    [Fact]
    public void Replace_EmptyOld_WithCount()
    {
        // Python: 'hello'.replace('', '-', 2) -> '-h-ello'
        StringExtensions.Replace("hello", "", "-", 2).Should().Be("-h-ello");
    }

    #endregion

    // ================================================================
    // Startswith
    // ================================================================

    #region Startswith

    [Fact]
    public void Startswith_BasicTrue()
    {
        "hello".Startswith("hel").Should().BeTrue();
    }

    [Fact]
    public void Startswith_BasicFalse()
    {
        "hello".Startswith("ell").Should().BeFalse();
    }

    [Fact]
    public void Startswith_WithStart()
    {
        "hello".Startswith("ell", 1).Should().BeTrue();
    }

    [Fact]
    public void Startswith_WithStartAndEnd_True()
    {
        "hello".Startswith("ell", 1, 4).Should().BeTrue();
    }

    [Fact]
    public void Startswith_WithStartAndEnd_False()
    {
        // "hello"[1:3] = "el", doesn't start with "ell"
        "hello".Startswith("ell", 1, 3).Should().BeFalse();
    }

    [Fact]
    public void Startswith_EmptyPrefix()
    {
        "hello".Startswith("").Should().BeTrue();
    }

    [Fact]
    public void Startswith_NegativeStart()
    {
        // Python: "hello".startswith("lo", -2) -> True
        "hello".Startswith("lo", -2).Should().BeTrue();
    }

    [Fact]
    public void Startswith_StartBeyondLength()
    {
        "hello".Startswith("", 10).Should().BeFalse();
    }

    #endregion

    // ================================================================
    // Endswith
    // ================================================================

    #region Endswith

    [Fact]
    public void Endswith_BasicTrue()
    {
        "hello".Endswith("llo").Should().BeTrue();
    }

    [Fact]
    public void Endswith_BasicFalse()
    {
        "hello".Endswith("ell").Should().BeFalse();
    }

    [Fact]
    public void Endswith_WithStartAndEnd()
    {
        // Python: "hello".endswith("ell", 0, 4) -> True ("hell" ends with "ell")
        "hello".Endswith("ell", 0, 4).Should().BeTrue();
    }

    [Fact]
    public void Endswith_EmptySuffix()
    {
        "hello".Endswith("").Should().BeTrue();
    }

    [Fact]
    public void Endswith_NegativeStart()
    {
        "hello".Endswith("llo", -3).Should().BeTrue();
    }

    [Fact]
    public void Endswith_PrefixTooLong()
    {
        "hi".Endswith("hello").Should().BeFalse();
    }

    [Fact]
    public void Endswith_WithStart()
    {
        // Python: "hello world".endswith("world", 6) -> True
        "hello world".Endswith("world", 6).Should().BeTrue();
    }

    #endregion

    // ================================================================
    // Index / Rindex
    // ================================================================

    #region Index

    [Fact]
    public void Index_Found()
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
        "hello hello".Index("hello", 0, 5).Should().Be(0);
    }

    [Fact]
    public void Index_WithStartAndEnd_NotFound()
    {
        var act = () => "hello hello".Index("hello", 1, 5);
        act.Should().Throw<ValueError>();
    }

    #endregion

    #region Rindex

    [Fact]
    public void Rindex_Found()
    {
        "hello hello".Rindex("hello").Should().Be(6);
    }

    [Fact]
    public void Rindex_NotFound_ThrowsValueError()
    {
        var act = () => "hello".Rindex("xyz");
        act.Should().Throw<ValueError>().WithMessage("substring not found");
    }

    [Fact]
    public void Rindex_WithStart()
    {
        "hello hello".Rindex("hello", 1).Should().Be(6);
    }

    [Fact]
    public void Rindex_WithStartAndEnd()
    {
        "hello hello".Rindex("hello", 0, 5).Should().Be(0);
    }

    #endregion

    // ================================================================
    // Partition / Rpartition
    // ================================================================

    #region Partition

    [Fact]
    public void Partition_Found()
    {
        "hello world hello".Partition(" ").Should().Be(("hello", " ", "world hello"));
    }

    [Fact]
    public void Partition_NotFound()
    {
        "hello".Partition(" ").Should().Be(("hello", "", ""));
    }

    [Fact]
    public void Partition_MultiCharSep()
    {
        "hello::world".Partition("::").Should().Be(("hello", "::", "world"));
    }

    [Fact]
    public void Partition_EmptySep_ThrowsValueError()
    {
        var act = () => "hello".Partition("");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Partition_SepAtStart()
    {
        " hello".Partition(" ").Should().Be(("", " ", "hello"));
    }

    [Fact]
    public void Partition_SepAtEnd()
    {
        "hello ".Partition(" ").Should().Be(("hello", " ", ""));
    }

    #endregion

    #region Rpartition

    [Fact]
    public void Rpartition_Found()
    {
        "hello world hello".Rpartition(" ").Should().Be(("hello world", " ", "hello"));
    }

    [Fact]
    public void Rpartition_NotFound()
    {
        "hello".Rpartition(" ").Should().Be(("", "", "hello"));
    }

    [Fact]
    public void Rpartition_MultiCharSep()
    {
        "hello::world::end".Rpartition("::").Should().Be(("hello::world", "::", "end"));
    }

    [Fact]
    public void Rpartition_EmptySep_ThrowsValueError()
    {
        var act = () => "hello".Rpartition("");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Rpartition_SepAtStart()
    {
        " hello".Rpartition(" ").Should().Be(("", " ", "hello"));
    }

    #endregion

    // ================================================================
    // Expandtabs
    // ================================================================

    #region Expandtabs

    [Fact]
    public void Expandtabs_Default()
    {
        // Python: '01\t012\t0123\t01234'.expandtabs() -> '01      012     0123    01234'
        "01\t012\t0123\t01234".Expandtabs().Should().Be("01      012     0123    01234");
    }

    [Fact]
    public void Expandtabs_Custom()
    {
        // Python: '01\t012\t0123\t01234'.expandtabs(4) -> '01  012 0123    01234'
        "01\t012\t0123\t01234".Expandtabs(4).Should().Be("01  012 0123    01234");
    }

    [Fact]
    public void Expandtabs_SingleTab()
    {
        // Python: '\t'.expandtabs() -> '        ' (8 spaces)
        "\t".Expandtabs().Should().Be("        ");
    }

    [Fact]
    public void Expandtabs_NoTabs()
    {
        "hello".Expandtabs().Should().Be("hello");
    }

    [Fact]
    public void Expandtabs_TabsizeZero()
    {
        "a\tb".Expandtabs(0).Should().Be("ab");
    }

    [Fact]
    public void Expandtabs_NewlineResets()
    {
        "a\tb\na\tb".Expandtabs(4).Should().Be("a   b\na   b");
    }

    #endregion

    // ================================================================
    // Istitle
    // ================================================================

    #region Istitle

    [Fact]
    public void Istitle_TitleCase_True()
    {
        "Hello World".Istitle().Should().BeTrue();
    }

    [Fact]
    public void Istitle_NotTitleCase_False()
    {
        "Hello world".Istitle().Should().BeFalse();
    }

    [Fact]
    public void Istitle_AllUpper_False()
    {
        "HELLO".Istitle().Should().BeFalse();
    }

    [Fact]
    public void Istitle_Empty_False()
    {
        "".Istitle().Should().BeFalse();
    }

    [Fact]
    public void Istitle_TrailingSpace_True()
    {
        "Hello ".Istitle().Should().BeTrue();
    }

    [Fact]
    public void Istitle_NumberBoundary_True()
    {
        // Python: 'Hello1World'.istitle() -> True (digits break casing)
        "Hello1World".Istitle().Should().BeTrue();
    }

    [Fact]
    public void Istitle_DigitThenUpper_True()
    {
        // Python: '3A'.istitle() -> True
        "3A".Istitle().Should().BeTrue();
    }

    #endregion

    // ================================================================
    // Encode
    // ================================================================

    #region Encode

    [Fact]
    public void Encode_Utf8_Default()
    {
        "hello".Encode().Should().Equal(0x68, 0x65, 0x6c, 0x6c, 0x6f);
    }

    [Fact]
    public void Encode_Utf8_Explicit()
    {
        "hello".Encode("utf-8").Should().Equal(0x68, 0x65, 0x6c, 0x6c, 0x6f);
    }

    [Fact]
    public void Encode_Ascii()
    {
        "hello".Encode("ascii").Should().Equal(0x68, 0x65, 0x6c, 0x6c, 0x6f);
    }

    [Fact]
    public void Encode_UnknownEncoding_ThrowsLookupError()
    {
        var act = () => "hello".Encode("bogus");
        act.Should().Throw<LookupError>();
    }

    [Fact]
    public void Encode_EmptyString()
    {
        "".Encode().Should().BeEmpty();
    }

    #endregion

    // ================================================================
    // Maketrans / Translate
    // ================================================================

    #region Maketrans / Translate

    [Fact]
    public void Maketrans_BasicMapping()
    {
        var table = StringExtensions.Maketrans("aeiou", "12345");
        "hello world".Translate(table).Should().Be("h2ll4 w4rld");
    }

    [Fact]
    public void Maketrans_WithDeletion()
    {
        var table = StringExtensions.Maketrans("", "", "aeiou");
        "hello world".Translate(table).Should().Be("hll wrld");
    }

    [Fact]
    public void Maketrans_CombinedMappingAndDeletion()
    {
        var table = StringExtensions.Maketrans("lo", "LO", "e");
        "hello world".Translate(table).Should().Be("hLLO wOrLd");
    }

    [Fact]
    public void Maketrans_UnequalLengths_ThrowsValueError()
    {
        var act = () => StringExtensions.Maketrans("abc", "de");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Translate_NoChanges()
    {
        var table = StringExtensions.Maketrans("x", "y");
        "hello".Translate(table).Should().Be("hello");
    }

    [Fact]
    public void Translate_EmptyString()
    {
        var table = StringExtensions.Maketrans("a", "b");
        "".Translate(table).Should().Be("");
    }

    #endregion
}
