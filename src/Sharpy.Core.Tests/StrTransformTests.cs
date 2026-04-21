using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Edge-case tests for string transform methods.
/// Basic "happy path" cases are already covered in StringExtensionTests.cs.
/// This file focuses on: overloads with extra args, edge cases, and empty strings.
/// </summary>
public class StrTransformTests
{
    #region Case methods — empty string

    [Fact]
    public void Upper_EmptyString_ReturnsEmpty()
    {
        "".Upper().Should().Be("");
    }

    [Fact]
    public void Lower_EmptyString_ReturnsEmpty()
    {
        "".Lower().Should().Be("");
    }

    [Fact]
    public void Capitalize_EmptyString_ReturnsEmpty()
    {
        "".Capitalize().Should().Be("");
    }

    [Fact]
    public void Swapcase_EmptyString_ReturnsEmpty()
    {
        "".Swapcase().Should().Be("");
    }

    [Fact]
    public void Casefold_EmptyString_ReturnsEmpty()
    {
        "".Casefold().Should().Be("");
    }

    [Fact]
    public void Title_EmptyString_ReturnsEmpty()
    {
        "".Title().Should().Be("");
    }

    #endregion

    #region Strip with chars overloads (not just Strip)

    [Fact]
    public void Lstrip_WithChars_RemovesLeadingChars()
    {
        // StringExtensionTests only tests Strip(chars), not Lstrip(chars)
        "xxhelloxx".Lstrip("x").Should().Be("helloxx");
    }

    [Fact]
    public void Rstrip_WithChars_RemovesTrailingChars()
    {
        // StringExtensionTests only tests Strip(chars), not Rstrip(chars)
        "xxhelloxx".Rstrip("x").Should().Be("xxhello");
    }

    [Fact]
    public void Strip_WithMultipleChars_RemovesAnyOfThem()
    {
        // Python: "abcba".strip("ab") == "c"
        "abcba".Strip("ab").Should().Be("c");
    }

    [Fact]
    public void Lstrip_EmptyString_ReturnsEmpty()
    {
        "".Lstrip("x").Should().Be("");
    }

    [Fact]
    public void Rstrip_EmptyString_ReturnsEmpty()
    {
        "".Rstrip("x").Should().Be("");
    }

    [Fact]
    public void Strip_EmptyCharsArg_RemovesNothing()
    {
        // Python: "  hello  ".strip("") == "  hello  " (empty chars = nothing stripped)
        "  hello  ".Strip("").Should().Be("  hello  ");
    }

    [Fact]
    public void Lstrip_EmptyCharsArg_RemovesNothing()
    {
        "  hello  ".Lstrip("").Should().Be("  hello  ");
    }

    [Fact]
    public void Rstrip_EmptyCharsArg_RemovesNothing()
    {
        "  hello  ".Rstrip("").Should().Be("  hello  ");
    }

    #endregion

    #region Justify methods — width less than or equal to string length

    [Fact]
    public void Center_WidthLessThanLength_ReturnsOriginal()
    {
        // Python: "hello".center(3) == "hello"
        "hello".Center(3).Should().Be("hello");
    }

    [Fact]
    public void Center_WidthEqualLength_ReturnsOriginal()
    {
        "hello".Center(5).Should().Be("hello");
    }

    [Fact]
    public void Ljust_WidthLessThanLength_ReturnsOriginal()
    {
        "hi".Ljust(1).Should().Be("hi");
    }

    [Fact]
    public void Rjust_WidthLessThanLength_ReturnsOriginal()
    {
        "hi".Rjust(1).Should().Be("hi");
    }

    [Fact]
    public void Ljust_WithFillChar()
    {
        "hi".Ljust(5, '-').Should().Be("hi---");
    }

    [Fact]
    public void Rjust_WithFillChar()
    {
        "hi".Rjust(5, '-').Should().Be("---hi");
    }

    [Fact]
    public void Center_OddPadding_ExtraOnRight()
    {
        // Python: "hi".center(5) == " hi  " — one extra pad on right when odd
        "hi".Center(5).Should().Be(" hi  ");
    }

    #endregion

    #region Zfill edge cases

    [Fact]
    public void Zfill_WidthLessThanLength_ReturnsOriginal()
    {
        // Python: "hello".zfill(3) == "hello"
        "hello".Zfill(3).Should().Be("hello");
    }

    [Fact]
    public void Zfill_PositiveSign_PreservesSign()
    {
        // Python: "+42".zfill(6) == "+00042"
        "+42".Zfill(6).Should().Be("+00042");
    }

    [Fact]
    public void Zfill_EmptyString_PadsWithZeros()
    {
        // Python: "".zfill(3) == "000"
        "".Zfill(3).Should().Be("000");
    }

    #endregion

    #region Replace edge cases

    [Fact]
    public void Replace_CountZero_ReturnsUnchanged()
    {
        // Python: "aaa".replace("a", "b", 0) == "aaa" (count=0 means no replacements)
        // Task description says "count=0 means all" — that is INCORRECT.
        StringExtensions.Replace("aaa", "a", "b", 0).Should().Be("aaa");
    }

    [Fact]
    public void Replace_CountNegative_ReplacesAll()
    {
        // Python: "aaa".replace("a", "b", -1) == "bbb"
        StringExtensions.Replace("aaa", "a", "b", -1).Should().Be("bbb");
    }

    [Fact]
    public void Replace_EmptyOldWithCountLimit_LimitsInsertions()
    {
        // Python: "abc".replace("", "-", 2) == "-a-bc"
        StringExtensions.Replace("abc", "", "-", 2).Should().Be("-a-bc");
    }

    [Fact]
    public void Replace_NotFound_ReturnsOriginal()
    {
        StringExtensions.Replace("hello", "xyz", "abc").Should().Be("hello");
    }

    #endregion

    #region Removeprefix/Removesuffix edge cases

    [Fact]
    public void Removeprefix_EmptyPrefix_ReturnsOriginal()
    {
        "hello".Removeprefix("").Should().Be("hello");
    }

    [Fact]
    public void Removesuffix_EmptyString_ReturnsOriginal()
    {
        "hello".Removesuffix("").Should().Be("hello");
    }

    [Fact]
    public void Removeprefix_PrefixLongerThanString_ReturnsOriginal()
    {
        "hi".Removeprefix("hello").Should().Be("hi");
    }

    [Fact]
    public void Removesuffix_SuffixLongerThanString_ReturnsOriginal()
    {
        "hi".Removesuffix("hello").Should().Be("hi");
    }

    #endregion

    #region Expandtabs edge cases

    [Fact]
    public void Expandtabs_TabsizeZero_RemovesTabs()
    {
        // Python: "a\tb".expandtabs(0) == "ab" (tabsize=0 means skip tabs)
        "a\tb".Expandtabs(0).Should().Be("ab");
    }

    [Fact]
    public void Expandtabs_TabsizeOne_ExpandsToOnespace()
    {
        // Python: "a\tb".expandtabs(1) == "a b"
        "a\tb".Expandtabs(1).Should().Be("a b");
    }

    [Fact]
    public void Expandtabs_AtColumnBoundary_AddsFullTabWidth()
    {
        // Python: "12345678\tb".expandtabs(8) == "12345678        b"
        // The tab at column 8 (a multiple of 8) expands to 8 spaces
        "12345678\tb".Expandtabs(8).Should().Be("12345678        b");
    }

    [Fact]
    public void Expandtabs_MultipleNewlines_ResetsColumn()
    {
        // After a newline, column count resets to 0
        "a\n\tb".Expandtabs(4).Should().Be("a\n    b");
    }

    #endregion

    #region Encode edge cases

    [Fact]
    public void Encode_EmptyString_ReturnsEmptyBytes()
    {
        var bytes = "".Encode();
        bytes.Length.Should().Be(0);
    }

    [Fact]
    public void Encode_Latin1Encoding_Works()
    {
        var bytes = "hello".Encode("latin-1");
        bytes.Length.Should().Be(5);
    }

    #endregion

    #region Capitalize edge cases

    [Fact]
    public void Capitalize_SingleChar_UppercasesIt()
    {
        "a".Capitalize().Should().Be("A");
    }

    [Fact]
    public void Capitalize_AllUppercase_LowercasesRest()
    {
        // Python: "HELLO".capitalize() == "Hello"
        "HELLO".Capitalize().Should().Be("Hello");
    }

    #endregion
}
