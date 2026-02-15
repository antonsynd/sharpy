using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for <see cref="StringExtensions"/> — the Python string method
/// equivalents exposed as C# extension methods on <see cref="string"/>.
/// </summary>
public class StringExtensionsTests
{
    #region Find

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
    public void Find_WithStart_SearchesFromStart()
    {
        "hello hello".Find("hello", 1).Should().Be(6);
    }

    [Fact]
    public void Find_WithNegativeStart_WrapsAround()
    {
        // Python: "hello".find("lo", -3) searches from index 2
        "hello".Find("lo", -3).Should().Be(3);
    }

    [Fact]
    public void Find_StartBeyondLength_ReturnsMinusOne()
    {
        // Python: "hello".find("lo", 10) → -1
        "hello".Find("lo", 10).Should().Be(-1);
    }

    [Fact]
    public void Find_EmptySubAtLength_ReturnsLength()
    {
        // Python: "hello".find("", 5) → 5
        "hello".Find("", 5).Should().Be(5);
    }

    [Fact]
    public void Find_EmptySubBeyondLength_ReturnsMinusOne()
    {
        // Python: "hello".find("", 10) → -1
        "hello".Find("", 10).Should().Be(-1);
    }

    [Fact]
    public void Find_WithStartAndEnd_SearchesSlice()
    {
        // Python: "hello world".find("world", 0, 11) → 6
        "hello world".Find("world", 0, 11).Should().Be(6);
    }

    [Fact]
    public void Find_WithEndCuttingOff_ReturnsMinusOne()
    {
        // Python: "hello world".find("world", 0, 10) → -1
        "hello world".Find("world", 0, 10).Should().Be(-1);
    }

    [Fact]
    public void Find_WithNegativeEnd_WrapsAround()
    {
        // Python: "hello".find("lo", 0, -1) → -1 (s[0:4] = "hell")
        "hello".Find("lo", 0, -1).Should().Be(-1);
    }

    [Fact]
    public void Find_EmptySlice_EmptySubReturnsStart()
    {
        // Python: "hello".find("", 5, 5) → 5
        "hello".Find("", 5, 5).Should().Be(5);
    }

    [Fact]
    public void Find_StartGtEnd_ReturnsMinusOne()
    {
        // Python: "hello".find("", 6, 5) → -1
        "hello".Find("", 6, 5).Should().Be(-1);
    }

    [Fact]
    public void Find_WithNegativeStartAndEnd()
    {
        // Python: "hello".find("lo", -3, 5) → 3
        "hello".Find("lo", -3, 5).Should().Be(3);
    }

    #endregion

    #region RFind

    [Fact]
    public void RFind_SubstringPresent_ReturnsLastIndex()
    {
        "hello hello".Rfind("hello").Should().Be(6);
    }

    [Fact]
    public void RFind_SubstringAbsent_ReturnsMinusOne()
    {
        "hello".Rfind("xyz").Should().Be(-1);
    }

    [Fact]
    public void RFind_WithStart_SearchesInSlice()
    {
        // Python: "hello hello".rfind("hello", 1) → 6
        "hello hello".Rfind("hello", 1).Should().Be(6);
    }

    [Fact]
    public void RFind_StartBeyondLength_ReturnsMinusOne()
    {
        // Python: "hello".rfind("lo", 10) → -1
        "hello".Rfind("lo", 10).Should().Be(-1);
    }

    [Fact]
    public void RFind_EmptySubAtLength_ReturnsLength()
    {
        // Python: "hello".rfind("", 5) → 5
        "hello".Rfind("", 5).Should().Be(5);
    }

    [Fact]
    public void RFind_EmptySubBeyondLength_ReturnsMinusOne()
    {
        // Python: "hello".rfind("", 6) → -1
        "hello".Rfind("", 6).Should().Be(-1);
    }

    [Fact]
    public void RFind_WithNegativeStart_WrapsAround()
    {
        // Python: "hello".rfind("lo", -3) → 3 (searches from index 2)
        "hello".Rfind("lo", -3).Should().Be(3);
    }

    [Fact]
    public void RFind_WithStartAndEnd_SearchesSlice()
    {
        // Python: "hello world".rfind("o", 0, 11) → 7
        "hello world".Rfind("o", 0, 11).Should().Be(7);
    }

    [Fact]
    public void RFind_WithEndLimited_FindsEarlierMatch()
    {
        // Python: "hello world".rfind("o", 0, 5) → 4
        "hello world".Rfind("o", 0, 5).Should().Be(4);
    }

    [Fact]
    public void RFind_WithNegativeEnd()
    {
        // Python: "hello".rfind("ell", 0, -1) → 1
        "hello".Rfind("ell", 0, -1).Should().Be(1);
    }

    [Fact]
    public void RFind_EmptySlice_EmptySubReturnsStart()
    {
        // Python: "hello".rfind("", 5, 5) → 5
        "hello".Rfind("", 5, 5).Should().Be(5);
    }

    [Fact]
    public void RFind_StartGtEnd_ReturnsMinusOne()
    {
        // Python: "hello".rfind("l", 5, 3) → -1
        "hello".Rfind("l", 5, 3).Should().Be(-1);
    }

    #endregion

    #region Strip

    [Fact]
    public void Strip_RemovesWhitespace()
    {
        "  hello  ".Strip().Should().Be("hello");
    }

    [Fact]
    public void Strip_WithChars_RemovesSpecifiedChars()
    {
        "xxhelloxx".Strip("x").Should().Be("hello");
    }

    #endregion

    #region Capitalize

    [Fact]
    public void Capitalize_FirstUpperRestLower()
    {
        "hELLO".Capitalize().Should().Be("Hello");
    }

    [Fact]
    public void Capitalize_EmptyString_ReturnsEmpty()
    {
        "".Capitalize().Should().Be("");
    }

    [Fact]
    public void Capitalize_SingleChar_ReturnsUpper()
    {
        "a".Capitalize().Should().Be("A");
    }

    #endregion

    #region Join

    [Fact]
    public void Join_WithStrings_JoinsSeparator()
    {
        ", ".Join(new[] { "a", "b", "c" }).Should().Be("a, b, c");
    }

    [Fact]
    public void Join_WithEmptySeparator_Concatenates()
    {
        "".Join(new[] { "a", "b", "c" }).Should().Be("abc");
    }

    #endregion

    #region Title

    [Fact]
    public void Title_CapitalizesWordBoundaries()
    {
        "hello world".Title().Should().Be("Hello World");
    }

    [Fact]
    public void Title_ApostropheIsWordBoundary()
    {
        // Python: "it's a test".title() → "It'S A Test"
        "it's a test".Title().Should().Be("It'S A Test");
    }

    #endregion

    #region Count

    [Fact]
    public void Count_NonOverlapping()
    {
        "aaa".Count("aa").Should().Be(1);
    }

    [Fact]
    public void Count_EmptySub_ReturnsLengthPlusOne()
    {
        // Python: "abc".count("") → 4
        "abc".Count("").Should().Be(4);
    }

    #endregion

    #region SplitLines

    [Fact]
    public void SplitLines_TrailingNewline()
    {
        // Python: "abc\n".splitlines() → ["abc"]
        "abc\n".Splitlines().Should().BeEquivalentTo(new[] { "abc" });
    }

    [Fact]
    public void SplitLines_MultipleLines()
    {
        "a\nb\nc".Splitlines().Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public void SplitLines_EmptyLinesBetween()
    {
        // Python: "\n\n".splitlines() → ["", ""]
        "\n\n".Splitlines().Should().BeEquivalentTo(new[] { "", "" });
    }

    [Fact]
    public void SplitLines_VerticalTab()
    {
        // Python: "a\x0bb".splitlines() → ["a", "b"]
        "a\u000Bb".Splitlines().Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void SplitLines_FormFeed()
    {
        // Python: "a\x0cb".splitlines() → ["a", "b"]
        "a\u000Cb".Splitlines().Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void SplitLines_NextLine()
    {
        // Python: "a\x85b".splitlines() → ["a", "b"]
        "a\u0085b".Splitlines().Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void SplitLines_LineSeparator()
    {
        // Python: "a\u2028b".splitlines() → ["a", "b"]
        "a\u2028b".Splitlines().Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void SplitLines_ParagraphSeparator()
    {
        // Python: "a\u2029b".splitlines() → ["a", "b"]
        "a\u2029b".Splitlines().Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void SplitLines_FileSeparator()
    {
        // Python: "a\x1cb".splitlines() → ["a", "b"]
        "a\u001Cb".Splitlines().Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void SplitLines_CarriageReturn()
    {
        // Python: "a\rb".splitlines() → ["a", "b"]
        "a\rb".Splitlines().Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void SplitLines_CrLf()
    {
        // Python: "a\r\nb".splitlines() → ["a", "b"]
        "a\r\nb".Splitlines().Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void Splitlines_KeepEnds_PreservesLineBreaks()
    {
        // Python: "hello\nworld\r\nfoo\rbar".splitlines(True) → ['hello\n', 'world\r\n', 'foo\r', 'bar']
        "hello\nworld\r\nfoo\rbar".Splitlines(true)
            .Should().Equal("hello\n", "world\r\n", "foo\r", "bar");
    }

    [Fact]
    public void Splitlines_KeepEnds_MixedLineEndings()
    {
        // Python: "a\nb\r\nc\rd".splitlines(True) → ['a\n', 'b\r\n', 'c\r', 'd']
        "a\nb\r\nc\rd".Splitlines(true)
            .Should().Equal("a\n", "b\r\n", "c\r", "d");
    }

    [Fact]
    public void Splitlines_KeepEnds_EmptyString()
    {
        // Python: "".splitlines(True) → []
        "".Splitlines(true).Should().BeEmpty();
    }

    [Fact]
    public void Splitlines_KeepEnds_NoLineBreaks()
    {
        // Python: "no newline".splitlines(True) → ['no newline']
        "no newline".Splitlines(true)
            .Should().Equal("no newline");
    }

    [Fact]
    public void Splitlines_KeepEnds_TrailingNewline()
    {
        // Python: "abc\n".splitlines(True) → ['abc\n']
        "abc\n".Splitlines(true)
            .Should().Equal("abc\n");
    }

    #endregion

    #region Swapcase

    [Fact]
    public void Swapcase_InvertsCasing()
    {
        // Python: "Hello World".swapcase() → "hELLO wORLD"
        "Hello World".Swapcase().Should().Be("hELLO wORLD");
    }

    [Fact]
    public void Swapcase_EmptyString_ReturnsEmpty()
    {
        "".Swapcase().Should().Be("");
    }

    #endregion

    #region Casefold

    [Fact]
    public void Casefold_ReturnsLowerInvariant()
    {
        "HELLO".Casefold().Should().Be("hello");
    }

    #endregion

    #region IsDigit / IsAlpha / IsAlnum / IsSpace

    [Fact]
    public void Isdigit_AllDigits_ReturnsTrue()
    {
        "123".Isdigit().Should().BeTrue();
    }

    [Fact]
    public void Isdigit_MixedContent_ReturnsFalse()
    {
        "12a".Isdigit().Should().BeFalse();
    }

    [Fact]
    public void Isdigit_EmptyString_ReturnsFalse()
    {
        "".Isdigit().Should().BeFalse();
    }

    [Fact]
    public void Isalpha_AllLetters_ReturnsTrue()
    {
        "abc".Isalpha().Should().BeTrue();
    }

    [Fact]
    public void Isalpha_MixedContent_ReturnsFalse()
    {
        "ab1".Isalpha().Should().BeFalse();
    }

    [Fact]
    public void Isalpha_EmptyString_ReturnsFalse()
    {
        "".Isalpha().Should().BeFalse();
    }

    [Fact]
    public void Isalnum_AlphaNumeric_ReturnsTrue()
    {
        "abc123".Isalnum().Should().BeTrue();
    }

    [Fact]
    public void Isalnum_WithSpaces_ReturnsFalse()
    {
        "ab c".Isalnum().Should().BeFalse();
    }

    [Fact]
    public void Isalnum_EmptyString_ReturnsFalse()
    {
        "".Isalnum().Should().BeFalse();
    }

    [Fact]
    public void Isspace_AllWhitespace_ReturnsTrue()
    {
        "   ".Isspace().Should().BeTrue();
    }

    [Fact]
    public void Isspace_MixedContent_ReturnsFalse()
    {
        "  a ".Isspace().Should().BeFalse();
    }

    [Fact]
    public void Isspace_EmptyString_ReturnsFalse()
    {
        "".Isspace().Should().BeFalse();
    }

    #endregion

    #region IsUpper / IsLower

    [Fact]
    public void Isupper_AllUpper_ReturnsTrue()
    {
        "ABC".Isupper().Should().BeTrue();
    }

    [Fact]
    public void Isupper_MixedCase_ReturnsFalse()
    {
        "ABc".Isupper().Should().BeFalse();
    }

    [Fact]
    public void Isupper_DigitsOnly_ReturnsFalse()
    {
        // Python: "123".isupper() → False (no cased characters)
        "123".Isupper().Should().BeFalse();
    }

    [Fact]
    public void Isupper_EmptyString_ReturnsFalse()
    {
        "".Isupper().Should().BeFalse();
    }

    [Fact]
    public void Islower_AllLower_ReturnsTrue()
    {
        "abc".Islower().Should().BeTrue();
    }

    [Fact]
    public void Islower_MixedCase_ReturnsFalse()
    {
        "aBc".Islower().Should().BeFalse();
    }

    [Fact]
    public void Islower_DigitsOnly_ReturnsFalse()
    {
        // Python: "123".islower() → False (no cased characters)
        "123".Islower().Should().BeFalse();
    }

    [Fact]
    public void Islower_EmptyString_ReturnsFalse()
    {
        "".Islower().Should().BeFalse();
    }

    #endregion

    #region Center / Ljust / Rjust

    [Fact]
    public void Center_PadsEvenly()
    {
        // Python: "hi".center(10) → "    hi    "
        "hi".Center(10).Should().Be("    hi    ");
    }

    [Fact]
    public void Center_WithFillChar()
    {
        // Python: "hi".center(10, "*") → "****hi****"
        "hi".Center(10, '*').Should().Be("****hi****");
    }

    [Fact]
    public void Center_WidthSmallerThanString_ReturnsOriginal()
    {
        "hello".Center(3).Should().Be("hello");
    }

    [Fact]
    public void Ljust_PadsRight()
    {
        // Python: "hi".ljust(10) → "hi        "
        "hi".Ljust(10).Should().Be("hi        ");
    }

    [Fact]
    public void Rjust_PadsLeft()
    {
        // Python: "hi".rjust(10) → "        hi"
        "hi".Rjust(10).Should().Be("        hi");
    }

    #endregion

    #region Zfill

    [Fact]
    public void Zfill_PadsWithZeros()
    {
        // Python: "42".zfill(5) → "00042"
        "42".Zfill(5).Should().Be("00042");
    }

    [Fact]
    public void Zfill_PreservesMinusSign()
    {
        // Python: "-42".zfill(5) → "-0042"
        "-42".Zfill(5).Should().Be("-0042");
    }

    [Fact]
    public void Zfill_PreservesPlusSign()
    {
        // Python: "+42".zfill(5) → "+0042"
        "+42".Zfill(5).Should().Be("+0042");
    }

    [Fact]
    public void Zfill_WidthSmallerThanString_ReturnsOriginal()
    {
        "hello".Zfill(3).Should().Be("hello");
    }

    #endregion

    #region Removeprefix / Removesuffix

    [Fact]
    public void Removeprefix_MatchingPrefix_RemovesIt()
    {
        // Python: "TestHook".removeprefix("Test") → "Hook"
        "TestHook".Removeprefix("Test").Should().Be("Hook");
    }

    [Fact]
    public void Removeprefix_NonMatchingPrefix_ReturnsOriginal()
    {
        // Python: "TestHook".removeprefix("Hook") → "TestHook"
        "TestHook".Removeprefix("Hook").Should().Be("TestHook");
    }

    [Fact]
    public void Removesuffix_MatchingSuffix_RemovesIt()
    {
        // Python: "MiscTests".removesuffix("Tests") → "Misc"
        "MiscTests".Removesuffix("Tests").Should().Be("Misc");
    }

    [Fact]
    public void Removesuffix_NonMatchingSuffix_ReturnsOriginal()
    {
        // Python: "MiscTests".removesuffix("Misc") → "MiscTests"
        "MiscTests".Removesuffix("Misc").Should().Be("MiscTests");
    }

    #endregion

    #region Upper / Lower (invariant culture)

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

    #endregion

    #region Lstrip / Rstrip

    [Fact]
    public void Lstrip_RemovesLeadingWhitespace()
    {
        "  hello  ".Lstrip().Should().Be("hello  ");
    }

    [Fact]
    public void Lstrip_WithChars_RemovesSpecifiedChars()
    {
        "aaabbbccc".Lstrip("a").Should().Be("bbbccc");
    }

    [Fact]
    public void Rstrip_RemovesTrailingWhitespace()
    {
        "  hello  ".Rstrip().Should().Be("  hello");
    }

    [Fact]
    public void Rstrip_WithChars_RemovesSpecifiedChars()
    {
        "aaabbbccc".Rstrip("c").Should().Be("aaabbb");
    }

    #endregion
}
