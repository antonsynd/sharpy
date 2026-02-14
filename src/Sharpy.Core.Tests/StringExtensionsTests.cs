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
        "hello hello".RFind("hello").Should().Be(6);
    }

    [Fact]
    public void RFind_SubstringAbsent_ReturnsMinusOne()
    {
        "hello".RFind("xyz").Should().Be(-1);
    }

    [Fact]
    public void RFind_WithStart_SearchesInSlice()
    {
        // Python: "hello hello".rfind("hello", 1) → 6
        "hello hello".RFind("hello", 1).Should().Be(6);
    }

    [Fact]
    public void RFind_StartBeyondLength_ReturnsMinusOne()
    {
        // Python: "hello".rfind("lo", 10) → -1
        "hello".RFind("lo", 10).Should().Be(-1);
    }

    [Fact]
    public void RFind_EmptySubAtLength_ReturnsLength()
    {
        // Python: "hello".rfind("", 5) → 5
        "hello".RFind("", 5).Should().Be(5);
    }

    [Fact]
    public void RFind_EmptySubBeyondLength_ReturnsMinusOne()
    {
        // Python: "hello".rfind("", 6) → -1
        "hello".RFind("", 6).Should().Be(-1);
    }

    [Fact]
    public void RFind_WithNegativeStart_WrapsAround()
    {
        // Python: "hello".rfind("lo", -3) → 3 (searches from index 2)
        "hello".RFind("lo", -3).Should().Be(3);
    }

    [Fact]
    public void RFind_WithStartAndEnd_SearchesSlice()
    {
        // Python: "hello world".rfind("o", 0, 11) → 7
        "hello world".RFind("o", 0, 11).Should().Be(7);
    }

    [Fact]
    public void RFind_WithEndLimited_FindsEarlierMatch()
    {
        // Python: "hello world".rfind("o", 0, 5) → 4
        "hello world".RFind("o", 0, 5).Should().Be(4);
    }

    [Fact]
    public void RFind_WithNegativeEnd()
    {
        // Python: "hello".rfind("ell", 0, -1) → 1
        "hello".RFind("ell", 0, -1).Should().Be(1);
    }

    [Fact]
    public void RFind_EmptySlice_EmptySubReturnsStart()
    {
        // Python: "hello".rfind("", 5, 5) → 5
        "hello".RFind("", 5, 5).Should().Be(5);
    }

    [Fact]
    public void RFind_StartGtEnd_ReturnsMinusOne()
    {
        // Python: "hello".rfind("l", 5, 3) → -1
        "hello".RFind("l", 5, 3).Should().Be(-1);
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
        "abc\n".SplitLines().Should().BeEquivalentTo(new[] { "abc" });
    }

    [Fact]
    public void SplitLines_MultipleLines()
    {
        "a\nb\nc".SplitLines().Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public void SplitLines_EmptyLinesBetween()
    {
        // Python: "\n\n".splitlines() → ["", ""]
        "\n\n".SplitLines().Should().BeEquivalentTo(new[] { "", "" });
    }

    #endregion
}
