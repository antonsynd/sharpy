using System.Linq;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for Python string methods implemented as extension methods on string.
/// </summary>
public class StringExtensionTests
{
    #region Equality

    [Fact]
    public void Equals_SameContent_ReturnsTrue()
    {
        string a = "hello";
        string b = "hello";
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentContent_ReturnsFalse()
    {
        string a = "hello";
        string b = "world";
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameContent_SameHash()
    {
        string a = "hello";
        string b = "hello";
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void OperatorEquals_SameContent()
    {
        string a = "hello";
        string b = "hello";
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_DifferentContent()
    {
        string a = "hello";
        string b = "world";
        (a != b).Should().BeTrue();
    }

    #endregion

    #region Comparison

    [Fact]
    public void CompareTo_LessThan()
    {
        string.Compare("abc", "abd", System.StringComparison.Ordinal).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_GreaterThan()
    {
        string.Compare("abd", "abc", System.StringComparison.Ordinal).Should().BePositive();
    }

    #endregion

    #region Concatenation

    [Fact]
    public void OperatorPlus_Concatenates()
    {
        string a = "hello";
        string b = " world";
        string result = a + b;
        result.Should().Be("hello world");
    }

    #endregion

    #region Repetition

    [Fact]
    public void Repeat_RepeatsString()
    {
        StringHelpers.Repeat("ab", 3).Should().Be("ababab");
    }

    [Fact]
    public void Repeat_ZeroCount_ReturnsEmpty()
    {
        StringHelpers.Repeat("ab", 0).Should().Be("");
    }

    [Fact]
    public void Repeat_NegativeCount_ReturnsEmpty()
    {
        StringHelpers.Repeat("ab", -1).Should().Be("");
    }

    [Fact]
    public void Repeat_One_ReturnsSame()
    {
        StringHelpers.Repeat("ab", 1).Should().Be("ab");
    }

    #endregion

    #region Indexing

    [Fact]
    public void GetItem_PositiveIndex()
    {
        StringHelpers.GetItem("hello", 0).Should().Be("h");
        StringHelpers.GetItem("hello", 4).Should().Be("o");
    }

    [Fact]
    public void GetItem_NegativeIndex()
    {
        StringHelpers.GetItem("hello", -1).Should().Be("o");
        StringHelpers.GetItem("hello", -5).Should().Be("h");
    }

    [Fact]
    public void GetItem_OutOfRange_ThrowsIndexError()
    {
        FluentActions.Invoking(() => StringHelpers.GetItem("hello", 5)).Should().Throw<IndexError>();
        FluentActions.Invoking(() => StringHelpers.GetItem("hello", -6)).Should().Throw<IndexError>();
    }

    #endregion

    #region Slicing

    [Fact]
    public void Slice_BasicSlice()
    {
        Slice.GetSlice("hello", 1, 3, null).Should().Be("el");
    }

    [Fact]
    public void Slice_NegativeIndices()
    {
        Slice.GetSlice("hello", -3, null, null).Should().Be("llo");
    }

    [Fact]
    public void Slice_Step()
    {
        Slice.GetSlice("hello", null, null, 2).Should().Be("hlo");
    }

    [Fact]
    public void Slice_ReverseStep()
    {
        Slice.GetSlice("hello", null, null, -1).Should().Be("olleh");
    }

    #endregion

    #region Iteration

    [Fact]
    public void Iterate_YieldsStringNotChar()
    {
        var items = StringHelpers.Iterate("abc").ToList();
        items.Should().HaveCount(3);
        items[0].Should().BeOfType<string>();
        items[0].Should().Be("a");
        items[1].Should().Be("b");
        items[2].Should().Be("c");
    }

    [Fact]
    public void Iterate_EmptyString_YieldsNothing()
    {
        StringHelpers.Iterate("").ToList().Should().BeEmpty();
    }

    #endregion

    #region Contains

    [Fact]
    public void Contains_SubstringPresent_ReturnsTrue()
    {
        StringExtensions.Contains("hello world", "world").Should().BeTrue();
    }

    [Fact]
    public void Contains_SubstringAbsent_ReturnsFalse()
    {
        StringExtensions.Contains("hello world", "xyz").Should().BeFalse();
    }

    [Fact]
    public void Contains_EmptySubstring_ReturnsTrue()
    {
        StringExtensions.Contains("hello", "").Should().BeTrue();
    }

    #endregion

    #region Reverse Iteration

    [Fact]
    public void Reversed_YieldsReversed()
    {
        var reversed = StringHelpers.Reversed("abc").ToList();
        reversed.Should().HaveCount(3);
        reversed[0].Should().Be("c");
        reversed[1].Should().Be("b");
        reversed[2].Should().Be("a");
    }

    #endregion

    #region Case Methods

    [Fact]
    public void Upper_ReturnsUppercase()
    {
        "hello".Upper().Should().Be("HELLO");
    }

    [Fact]
    public void Lower_ReturnsLowercase()
    {
        "HELLO".Lower().Should().Be("hello");
    }

    [Fact]
    public void Capitalize_FirstCharUpper()
    {
        "hello world".Capitalize().Should().Be("Hello world");
    }

    [Fact]
    public void Title_TitlecasesWords()
    {
        "hello world".Title().Should().Be("Hello World");
    }

    [Fact]
    public void Swapcase_SwapsCase()
    {
        "Hello World".Swapcase().Should().Be("hELLO wORLD");
    }

    [Fact]
    public void Casefold_FoldsCase()
    {
        "Straße".Casefold().Should().Be("strasse");
    }

    #endregion

    #region Strip Methods

    [Fact]
    public void Strip_RemovesWhitespace()
    {
        "  hello  ".Strip().Should().Be("hello");
    }

    [Fact]
    public void Strip_WithChars()
    {
        "xxhelloxx".Strip("x").Should().Be("hello");
    }

    [Fact]
    public void Lstrip_RemovesLeadingWhitespace()
    {
        "  hello".Lstrip().Should().Be("hello");
    }

    [Fact]
    public void Rstrip_RemovesTrailingWhitespace()
    {
        "hello  ".Rstrip().Should().Be("hello");
    }

    #endregion

    #region Justify Methods

    [Fact]
    public void Center_CentersString()
    {
        "hi".Center(10).Should().Be("    hi    ");
    }

    [Fact]
    public void Center_WithFillchar()
    {
        "hi".Center(10, '-').Should().Be("----hi----");
    }

    [Fact]
    public void Ljust_LeftJustifies()
    {
        "hi".Ljust(5).Should().Be("hi   ");
    }

    [Fact]
    public void Rjust_RightJustifies()
    {
        "hi".Rjust(5).Should().Be("   hi");
    }

    [Fact]
    public void Zfill_PadsWithZeros()
    {
        "42".Zfill(5).Should().Be("00042");
    }

    [Fact]
    public void Zfill_PreservesSign()
    {
        "-42".Zfill(5).Should().Be("-0042");
    }

    #endregion

    #region Prefix/Suffix Methods

    [Fact]
    public void Removeprefix_RemovesPrefix()
    {
        "HelloWorld".Removeprefix("Hello").Should().Be("World");
    }

    [Fact]
    public void Removeprefix_NoMatch_ReturnsOriginal()
    {
        "HelloWorld".Removeprefix("Bye").Should().Be("HelloWorld");
    }

    [Fact]
    public void Removesuffix_RemovesSuffix()
    {
        "HelloWorld".Removesuffix("World").Should().Be("Hello");
    }

    #endregion

    #region Replace

    [Fact]
    public void Replace_AllOccurrences()
    {
        StringExtensions.Replace("hello world", "world", "there").Should().Be("hello there");
    }

    [Fact]
    public void Replace_WithCount()
    {
        StringExtensions.Replace("aaa", "a", "b", 2).Should().Be("bba");
    }

    [Fact]
    public void Replace_EmptyOld_InsertsBetweenChars()
    {
        StringExtensions.Replace("ab", "", "-").Should().Be("-a-b-");
    }

    #endregion

    #region Find/Rfind

    [Fact]
    public void Find_SubstringPresent_ReturnsIndex()
    {
        "hello".Find("lo").Should().Be(3);
    }

    [Fact]
    public void Find_SubstringAbsent_ReturnsMinusOne()
    {
        "hello".Find("xyz").Should().Be(-1);
    }

    [Fact]
    public void Rfind_FindsLastOccurrence()
    {
        "hello hello".Rfind("hello").Should().Be(6);
    }

    #endregion

    #region Index/Rindex

    [Fact]
    public void Index_SubstringPresent_ReturnsIndex()
    {
        "hello".Index("lo").Should().Be(3);
    }

    [Fact]
    public void Index_SubstringAbsent_ThrowsValueError()
    {
        FluentActions.Invoking(() => "hello".Index("xyz")).Should().Throw<ValueError>();
    }

    #endregion

    #region Count

    [Fact]
    public void Count_NonOverlapping()
    {
        "banana".Count("an").Should().Be(2);
    }

    [Fact]
    public void Count_EmptySubstring()
    {
        "hello".Count("").Should().Be(6); // len + 1
    }

    #endregion

    #region Startswith/Endswith

    [Fact]
    public void Startswith_True()
    {
        "hello".Startswith("he").Should().BeTrue();
    }

    [Fact]
    public void Endswith_True()
    {
        "hello".Endswith("lo").Should().BeTrue();
    }

    #endregion

    #region Predicates

    [Fact]
    public void Isdigit_AllDigits_True()
    {
        "123".Isdigit().Should().BeTrue();
    }

    [Fact]
    public void Isdigit_WithDot_False()
    {
        "12.3".Isdigit().Should().BeFalse();
    }

    [Fact]
    public void Isalpha_AllLetters_True()
    {
        "hello".Isalpha().Should().BeTrue();
    }

    [Fact]
    public void Isalnum_MixedAlphaNum_True()
    {
        "abc123".Isalnum().Should().BeTrue();
    }

    [Fact]
    public void Isspace_AllWhitespace_True()
    {
        "  \t\n".Isspace().Should().BeTrue();
    }

    [Fact]
    public void Isupper_AllUpper_True()
    {
        "HELLO".Isupper().Should().BeTrue();
    }

    [Fact]
    public void Islower_AllLower_True()
    {
        "hello".Islower().Should().BeTrue();
    }

    [Fact]
    public void Istitle_TitleCase_True()
    {
        "Hello World".Istitle().Should().BeTrue();
    }

    [Fact]
    public void Isnumeric_Digits_True()
    {
        "123".Isnumeric().Should().BeTrue();
    }

    [Fact]
    public void Isdecimal_Digits_True()
    {
        "123".Isdecimal().Should().BeTrue();
    }

    [Fact]
    public void Isidentifier_ValidId_True()
    {
        "my_var".Isidentifier().Should().BeTrue();
    }

    [Fact]
    public void Isidentifier_StartsWithDigit_False()
    {
        "1abc".Isidentifier().Should().BeFalse();
    }

    [Fact]
    public void Isprintable_Printable_True()
    {
        "hello".Isprintable().Should().BeTrue();
    }

    [Fact]
    public void Isprintable_ControlChar_False()
    {
        "\x00".Isprintable().Should().BeFalse();
    }

    [Fact]
    public void Isprintable_Empty_True()
    {
        "".Isprintable().Should().BeTrue();
    }

    [Fact]
    public void Isascii_AllAscii_True()
    {
        "hello".Isascii().Should().BeTrue();
    }

    [Fact]
    public void Isascii_NonAscii_False()
    {
        "héllo".Isascii().Should().BeFalse();
    }

    [Fact]
    public void Isascii_Empty_True()
    {
        "".Isascii().Should().BeTrue();
    }

    #endregion

    #region Split

    [Fact]
    public void Split_OnWhitespace()
    {
        var result = StringExtensions.Split("a b  c");
        result.Should().HaveCount(3);
        result[0].Should().Be("a");
        result[1].Should().Be("b");
        result[2].Should().Be("c");
    }

    [Fact]
    public void Split_OnSeparator()
    {
        var result = StringExtensions.Split("a,b,c", ",");
        result.Should().HaveCount(3);
        result[0].Should().Be("a");
    }

    [Fact]
    public void Split_WithMaxsplit()
    {
        var result = StringExtensions.Split("a,b,c,d", ",", 2);
        result.Should().HaveCount(3);
        result[2].Should().Be("c,d");
    }

    [Fact]
    public void Split_EmptySep_ThrowsValueError()
    {
        FluentActions.Invoking(() => StringExtensions.Split("abc", "")).Should().Throw<ValueError>();
    }

    #endregion

    #region Rsplit

    [Fact]
    public void Rsplit_WithMaxsplit()
    {
        var result = StringExtensions.Rsplit("a,b,c,d", ",", 2);
        result.Should().HaveCount(3);
        result[0].Should().Be("a,b");
    }

    #endregion

    #region Splitlines

    [Fact]
    public void Splitlines_BasicNewlines()
    {
        var result = "a\nb\nc".Splitlines();
        result.Should().HaveCount(3);
        result[0].Should().Be("a");
    }

    [Fact]
    public void Splitlines_KeepEnds()
    {
        var result = "a\nb\n".Splitlines(true);
        result.Should().HaveCount(2);
        result[0].Should().Be("a\n");
    }

    #endregion

    #region Partition

    [Fact]
    public void Partition_Found()
    {
        var (before, sep, after) = "a.b.c".Partition(".");
        before.Should().Be("a");
        sep.Should().Be(".");
        after.Should().Be("b.c");
    }

    [Fact]
    public void Partition_NotFound()
    {
        var (before, sep, after) = "abc".Partition(".");
        before.Should().Be("abc");
        sep.Should().Be("");
        after.Should().Be("");
    }

    [Fact]
    public void Rpartition_Found()
    {
        var (before, sep, after) = "a.b.c".Rpartition(".");
        before.Should().Be("a.b");
        sep.Should().Be(".");
        after.Should().Be("c");
    }

    #endregion

    #region Join

    [Fact]
    public void Join_StringIterable()
    {
        var items = new string[] { "a", "b", "c" };
        ", ".Join(items).Should().Be("a, b, c");
    }

    #endregion

    #region Expandtabs

    [Fact]
    public void Expandtabs_Default()
    {
        string result = "a\tb".Expandtabs();
        result.Should().Contain("a");
        result.Should().Contain("b");
        result.Should().NotContain("\t");
    }

    [Fact]
    public void Expandtabs_Custom()
    {
        "a\tb".Expandtabs(4).Should().Be("a   b");
    }

    #endregion

    #region Encode

    [Fact]
    public void Encode_Utf8_ReturnsBytes()
    {
        var bytes = "hello".Encode();
        bytes.Length.Should().Be(5);
    }

    [Fact]
    public void Encode_Ascii()
    {
        var bytes = "hello".Encode("ascii");
        bytes.Length.Should().Be(5);
    }

    #endregion

    #region Maketrans / Translate

    [Fact]
    public void Maketrans_And_Translate()
    {
        var table = StringExtensions.Maketrans("aeiou", "12345");
        "apple".Translate(table).Should().Be("1ppl2");
    }

    #endregion
}
