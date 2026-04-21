using System.Collections.Generic;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Edge-case and overload tests for string search and split methods.
/// Basic "happy path" cases are covered in StringExtensionTests.cs.
/// This file focuses on: multi-arg overloads, Rindex (uncovered), and edge cases.
/// </summary>
public class StrSearchSplitTests
{
    #region Find — start and start+end overloads

    [Fact]
    public void Find_WithStart_SearchesFromOffset()
    {
        // Python: "hello world hello".find("hello", 1) == 12
        "hello world hello".Find("hello", 1).Should().Be(12);
    }

    [Fact]
    public void Find_WithStartAndEnd_SearchesWithinSlice()
    {
        // Python: "hello world hello".find("hello", 1, 10) == -1
        "hello world hello".Find("hello", 1, 10).Should().Be(-1);
    }

    [Fact]
    public void Find_WithNegativeStart_CountsFromEnd()
    {
        // Python: "hello".find("lo", -3) == 3
        "hello".Find("lo", -3).Should().Be(3);
    }

    [Fact]
    public void Find_EmptySubstringAtStart_ReturnsZero()
    {
        "hello".Find("", 0).Should().Be(0);
    }

    [Fact]
    public void Find_EmptySubstringAtEnd_ReturnsLength()
    {
        // Python: "hello".find("", 5) == 5
        "hello".Find("", 5).Should().Be(5);
    }

    [Fact]
    public void Find_StartBeyondLength_ReturnsMinusOne()
    {
        "hello".Find("lo", 10).Should().Be(-1);
    }

    #endregion

    #region Rfind — start and start+end overloads

    [Fact]
    public void Rfind_WithStartAndEnd_SearchesWithinSlice()
    {
        // Python: "hello world hello".rfind("hello", 0, 12) == 0
        "hello world hello".Rfind("hello", 0, 12).Should().Be(0);
    }

    [Fact]
    public void Rfind_WithStart_SearchesFromOffset()
    {
        // Python: "hello hello".rfind("hello", 0) == 6
        "hello hello".Rfind("hello", 0).Should().Be(6);
    }

    [Fact]
    public void Rfind_WithNegativeStart_CountsFromEnd()
    {
        // Python: "abcabc".rfind("bc", -4) == 4
        "abcabc".Rfind("bc", -4).Should().Be(4);
    }

    [Fact]
    public void Rfind_EmptySubstring_ReturnsEnd()
    {
        // Python: "hello".rfind("", 0, 3) == 3
        "hello".Rfind("", 0, 3).Should().Be(3);
    }

    #endregion

    #region Index — start and start+end overloads

    [Fact]
    public void Index_WithStart_SearchesFromOffset()
    {
        "hello world hello".Index("hello", 1).Should().Be(12);
    }

    [Fact]
    public void Index_WithStartAndEnd_SearchesWithinSlice()
    {
        FluentActions.Invoking(() => "hello world hello".Index("hello", 1, 10))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Index_WithStart_SubstringPresent_ReturnsIndex()
    {
        "abcabc".Index("bc", 2).Should().Be(4);
    }

    #endregion

    #region Rindex — entirely uncovered in existing tests

    [Fact]
    public void Rindex_SubstringPresent_ReturnsLastIndex()
    {
        // Python: "hello world hello".rindex("hello") == 12
        "hello world hello".Rindex("hello").Should().Be(12);
    }

    [Fact]
    public void Rindex_SubstringAbsent_ThrowsValueError()
    {
        FluentActions.Invoking(() => "hello".Rindex("xyz"))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Rindex_WithStart_SearchesFromOffset()
    {
        // Python: "hello hello".rindex("hello", 1) == 6
        "hello hello".Rindex("hello", 1).Should().Be(6);
    }

    [Fact]
    public void Rindex_WithStartAndEnd_SearchesWithinSlice()
    {
        // Python: "hello world hello".rindex("hello", 0, 12) == 0
        "hello world hello".Rindex("hello", 0, 12).Should().Be(0);
    }

    [Fact]
    public void Rindex_WithStartAndEnd_NotFound_ThrowsValueError()
    {
        FluentActions.Invoking(() => "hello world hello".Rindex("hello", 1, 10))
            .Should().Throw<ValueError>();
    }

    #endregion

    #region Count — edge cases (only basic overload exists)

    [Fact]
    public void Count_NotFound_ReturnsZero()
    {
        "hello".Count("xyz").Should().Be(0);
    }

    [Fact]
    public void Count_EmptyString_EmptySub_ReturnsOne()
    {
        // Python: "".count("") == 1
        "".Count("").Should().Be(1);
    }

    #endregion

    #region Startswith — start and start+end overloads

    [Fact]
    public void Startswith_WithStart_ChecksFromOffset()
    {
        // Python: "hello".startswith("he", 0) == True
        "hello".Startswith("he", 0).Should().BeTrue();
    }

    [Fact]
    public void Startswith_WithStart_PrefixMissed_ReturnsFalse()
    {
        // Python: "hello".startswith("he", 1) == False
        "hello".Startswith("he", 1).Should().BeFalse();
    }

    [Fact]
    public void Startswith_WithStartAndEnd_PrefixPresent_ReturnsTrue()
    {
        // Python: "hello".startswith("ell", 1, 4) == True
        "hello".Startswith("ell", 1, 4).Should().BeTrue();
    }

    [Fact]
    public void Startswith_WithStartAndEnd_PrefixLongerThanSlice_ReturnsFalse()
    {
        // Python: "hello".startswith("ello", 1, 4) == False (prefix longer than slice)
        "hello".Startswith("ello", 1, 4).Should().BeFalse();
    }

    [Fact]
    public void Startswith_EmptyPrefix_ReturnsTrue()
    {
        "hello".Startswith("", 2).Should().BeTrue();
    }

    #endregion

    #region Endswith — start and start+end overloads

    [Fact]
    public void Endswith_WithStart_ChecksFromOffset()
    {
        // Python: "hello".endswith("lo", 0) == True
        "hello".Endswith("lo", 0).Should().BeTrue();
    }

    [Fact]
    public void Endswith_WithStartAndEnd_SuffixPresent_ReturnsTrue()
    {
        // Python: "hello".endswith("lo", 0, 5) == True
        "hello".Endswith("lo", 0, 5).Should().BeTrue();
    }

    [Fact]
    public void Endswith_WithStartAndEnd_SuffixMissed_ReturnsFalse()
    {
        // Python: "hello".endswith("lo", 0, 4) == False (slice doesn't include 'o')
        "hello".Endswith("lo", 0, 4).Should().BeFalse();
    }

    [Fact]
    public void Endswith_EmptySuffix_ReturnsTrue()
    {
        "hello".Endswith("", 0, 3).Should().BeTrue();
    }

    #endregion

    #region Rsplit — no args and no-maxsplit

    [Fact]
    public void Rsplit_NoArgs_SameAsWhitespaceSplit()
    {
        // Python: "a b  c".rsplit() == ["a", "b", "c"] (same as split())
        var result = StringExtensions.Rsplit("a b  c");
        result.Should().HaveCount(3);
        result[0].Should().Be("a");
        result[1].Should().Be("b");
        result[2].Should().Be("c");
    }

    [Fact]
    public void Rsplit_WithSepNoMaxsplit_SplitsAll()
    {
        // Python: "a,b,c".rsplit(",") == ["a", "b", "c"]
        var result = StringExtensions.Rsplit("a,b,c", ",");
        result.Should().HaveCount(3);
        result[0].Should().Be("a");
        result[2].Should().Be("c");
    }

    #endregion

    #region Splitlines — edge cases

    [Fact]
    public void Splitlines_EmptyString_ReturnsEmptyList()
    {
        // Python: "".splitlines() == []
        StringExtensions.Splitlines("").Should().BeEmpty();
    }

    [Fact]
    public void Splitlines_CarriageReturnLineFeed_TreatedAsOne()
    {
        // Python: "a\r\nb".splitlines() == ["a", "b"]
        var result = "a\r\nb".Splitlines();
        result.Should().HaveCount(2);
        result[0].Should().Be("a");
        result[1].Should().Be("b");
    }

    [Fact]
    public void Splitlines_TrailingNewline_NotExtraEmpty()
    {
        // Python: "a\n".splitlines() == ["a"]  (no trailing empty string)
        var result = "a\n".Splitlines();
        result.Should().HaveCount(1);
        result[0].Should().Be("a");
    }

    [Fact]
    public void Splitlines_KeepEnds_WithCRLF_IncludesBoth()
    {
        // Python: "a\r\nb".splitlines(True) == ["a\r\n", "b"]
        var result = "a\r\nb".Splitlines(true);
        result.Should().HaveCount(2);
        result[0].Should().Be("a\r\n");
        result[1].Should().Be("b");
    }

    #endregion

    #region Join — edge cases

    [Fact]
    public void Join_EmptyIterable_ReturnsEmpty()
    {
        ", ".Join(new List<string>()).Should().Be("");
    }

    [Fact]
    public void Join_SingleItem_ReturnsItem()
    {
        ", ".Join(new List<string> { "hello" }).Should().Be("hello");
    }

    [Fact]
    public void Join_EmptySeparator_Concatenates()
    {
        "".Join(new List<string> { "a", "b", "c" }).Should().Be("abc");
    }

    #endregion

    #region Partition — edge cases

    [Fact]
    public void Partition_EmptyString_NotFound_ReturnsTriple()
    {
        var (before, sep, after) = "".Partition(".");
        before.Should().Be("");
        sep.Should().Be("");
        after.Should().Be("");
    }

    [Fact]
    public void Rpartition_NotFound_PutsStringAtEnd()
    {
        // Python: "abc".rpartition(".") == ("", "", "abc")
        var (before, sep, after) = "abc".Rpartition(".");
        before.Should().Be("");
        sep.Should().Be("");
        after.Should().Be("abc");
    }

    #endregion
}
