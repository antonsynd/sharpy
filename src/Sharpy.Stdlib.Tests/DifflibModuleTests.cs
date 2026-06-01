using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests;

public class DifflibModuleTests
{
    [Fact]
    public void SequenceMatcher_Ratio_MatchesPython()
    {
        var sm = new SequenceMatcher<char>(null, "abcde".ToCharArray(), "abdce".ToCharArray());
        sm.Ratio().Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public void SequenceMatcher_GetMatchingBlocks_ReturnsCorrectBlocks()
    {
        var sm = new SequenceMatcher<char>(null, "abcde".ToCharArray(), "abdce".ToCharArray());
        var blocks = sm.GetMatchingBlocks();
        blocks[blocks.Count - 1].Should().Be((5, 5, 0));
        blocks.Sum(b => b.size).Should().Be(4);
    }

    [Fact]
    public void SequenceMatcher_GetOpcodes_ReturnsCorrectTags()
    {
        var sm = new SequenceMatcher<char>(null, "abcde".ToCharArray(), "abdce".ToCharArray());
        var opcodes = sm.GetOpcodes();
        opcodes.Should().Contain(o => o.tag == "equal");
        opcodes.Should().Contain(o => o.tag == "insert" || o.tag == "delete" || o.tag == "replace");
    }

    [Fact]
    public void SequenceMatcher_QuickRatio_IsUpperBound()
    {
        var sm = new SequenceMatcher<char>(null, "abcde".ToCharArray(), "abdce".ToCharArray());
        sm.QuickRatio().Should().BeGreaterThanOrEqualTo(sm.Ratio());
    }

    [Fact]
    public void SequenceMatcher_RealQuickRatio_IsUpperBound()
    {
        var sm = new SequenceMatcher<char>(null, "abcde".ToCharArray(), "abdce".ToCharArray());
        sm.RealQuickRatio().Should().BeGreaterThanOrEqualTo(sm.QuickRatio());
    }

    [Fact]
    public void SequenceMatcher_EmptySequences_RatioIsOne()
    {
        var sm = new SequenceMatcher<char>(null, Array.Empty<char>(), Array.Empty<char>());
        sm.Ratio().Should().Be(1.0);
    }

    [Fact]
    public void SequenceMatcher_OneEmpty_RatioIsZero()
    {
        var sm = new SequenceMatcher<char>(null, "abc".ToCharArray(), Array.Empty<char>());
        sm.Ratio().Should().Be(0.0);
    }

    [Fact]
    public void SequenceMatcher_JunkFunction_ExcludesElements()
    {
        Func<char, bool> isJunk = c => c == ' ';
        var sm = new SequenceMatcher<char>(isJunk, "a b c".ToCharArray(), "a  b  c".ToCharArray());
        sm.Ratio().Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void SequenceMatcher_SetSeqs_ResetsState()
    {
        var sm = new SequenceMatcher<char>(null, "abc".ToCharArray(), "abc".ToCharArray());
        sm.Ratio().Should().Be(1.0);
        sm.SetSeqs("abc".ToCharArray(), "xyz".ToCharArray());
        sm.Ratio().Should().BeLessThan(1.0);
    }

    [Fact]
    public void SequenceMatcher_IdenticalSequences_RatioIsOne()
    {
        var sm = new SequenceMatcher<string>(null,
            new[] { "line1\n", "line2\n" },
            new[] { "line1\n", "line2\n" });
        sm.Ratio().Should().Be(1.0);
    }

    [Fact]
    public void GetCloseMatches_MatchesPython()
    {
        var result = DifflibModule.GetCloseMatches("appel",
            new List<string> { "ape", "apple", "peach" });
        result.Should().Equal("apple", "ape");
    }

    [Fact]
    public void GetCloseMatches_HighCutoff_FiltersWorse()
    {
        var result = DifflibModule.GetCloseMatches("appel",
            new List<string> { "ape", "apple", "peach" }, cutoff: 0.9);
        result.Should().NotContain("ape");
    }

    [Fact]
    public void GetCloseMatches_N1_ReturnsBestOnly()
    {
        var result = DifflibModule.GetCloseMatches("appel",
            new List<string> { "ape", "apple", "peach" }, n: 1);
        result.Should().HaveCount(1);
        result[0].Should().Be("apple");
    }

    [Fact]
    public void GetCloseMatches_NoMatches_ReturnsEmpty()
    {
        var result = DifflibModule.GetCloseMatches("xyz",
            new List<string> { "abc", "def" }, cutoff: 0.9);
        result.Should().BeEmpty();
    }

    [Fact]
    public void IsLineJunk_BlankLine_ReturnsTrue()
    {
        DifflibModule.IsLineJunk("  \n").Should().BeTrue();
    }

    [Fact]
    public void IsLineJunk_CommentLine_ReturnsTrue()
    {
        DifflibModule.IsLineJunk("  #  \n").Should().BeTrue();
    }

    [Fact]
    public void IsLineJunk_CodeLine_ReturnsFalse()
    {
        DifflibModule.IsLineJunk("code\n").Should().BeFalse();
    }

    [Fact]
    public void IsCharacterJunk_Space_ReturnsTrue()
    {
        DifflibModule.IsCharacterJunk(" ").Should().BeTrue();
    }

    [Fact]
    public void IsCharacterJunk_Tab_ReturnsTrue()
    {
        DifflibModule.IsCharacterJunk("\t").Should().BeTrue();
    }

    [Fact]
    public void IsCharacterJunk_Newline_ReturnsFalse()
    {
        DifflibModule.IsCharacterJunk("\n").Should().BeFalse();
    }

    [Fact]
    public void IsCharacterJunk_Letter_ReturnsFalse()
    {
        DifflibModule.IsCharacterJunk("a").Should().BeFalse();
    }

    [Fact]
    public void UnifiedDiff_BasicOutput_HasCorrectFormat()
    {
        var a = new List<string> { "one\n", "two\n", "three\n" };
        var b = new List<string> { "one\n", "tree\n", "three\n" };
        var diff = DifflibModule.UnifiedDiff(a, b, "a.txt", "b.txt").ToList();
        diff.Should().Contain(l => l.StartsWith("--- "));
        diff.Should().Contain(l => l.StartsWith("+++ "));
        diff.Should().Contain(l => l.StartsWith("@@ "));
    }

    [Fact]
    public void UnifiedDiff_IdenticalInputs_NoOutput()
    {
        var a = new List<string> { "one\n", "two\n" };
        var diff = DifflibModule.UnifiedDiff(a, a).ToList();
        diff.Should().BeEmpty();
    }

    [Fact]
    public void ContextDiff_HasCorrectFormat()
    {
        var a = new List<string> { "one\n", "two\n", "three\n" };
        var b = new List<string> { "one\n", "tree\n", "three\n" };
        var diff = DifflibModule.ContextDiff(a, b, "a.txt", "b.txt").ToList();
        diff.Should().Contain(l => l.StartsWith("*** "));
        diff.Should().Contain(l => l.StartsWith("--- "));
    }

    [Fact]
    public void Ndiff_CommonLines_PrefixedWithSpace()
    {
        var a = new List<string> { "one\n", "two\n" };
        var b = new List<string> { "one\n", "two\n" };
        var diff = DifflibModule.Ndiff(a, b).ToList();
        diff.Should().OnlyContain(l => l.StartsWith("  "));
    }

    [Fact]
    public void Ndiff_RemovedLines_PrefixedWithMinus()
    {
        var a = new List<string> { "one\n", "two\n" };
        var b = new List<string> { "one\n" };
        var diff = DifflibModule.Ndiff(a, b).ToList();
        diff.Should().Contain(l => l.StartsWith("- "));
    }

    [Fact]
    public void Ndiff_AddedLines_PrefixedWithPlus()
    {
        var a = new List<string> { "one\n" };
        var b = new List<string> { "one\n", "two\n" };
        var diff = DifflibModule.Ndiff(a, b).ToList();
        diff.Should().Contain(l => l.StartsWith("+ "));
    }

    [Fact]
    public void Differ_Compare_ProducesDelta()
    {
        var d = new Differ();
        var a = new List<string> { "one\n", "two\n", "three\n" };
        var b = new List<string> { "ore\n", "tree\n", "emu\n" };
        var result = d.Compare(a, b).ToList();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void Restore_Which1_RecoversA()
    {
        var a = new List<string> { "one\n", "two\n", "three\n" };
        var b = new List<string> { "ore\n", "tree\n", "emu\n" };
        var diff = DifflibModule.Ndiff(a, b).ToList();
        var restored = DifflibModule.Restore(diff, 1).ToList();
        restored.Should().Equal(a);
    }

    [Fact]
    public void Restore_Which2_RecoversB()
    {
        var a = new List<string> { "one\n", "two\n", "three\n" };
        var b = new List<string> { "ore\n", "tree\n", "emu\n" };
        var diff = DifflibModule.Ndiff(a, b).ToList();
        var restored = DifflibModule.Restore(diff, 2).ToList();
        restored.Should().Equal(b);
    }

    [Fact]
    public void Restore_InvalidWhich_Throws()
    {
        var act = () => DifflibModule.Restore(new[] { "  line" }, 3).ToList();
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void UnifiedDiff_WithFileNames_InHeader()
    {
        var a = new List<string> { "x\n" };
        var b = new List<string> { "y\n" };
        var diff = DifflibModule.UnifiedDiff(a, b, "old.txt", "new.txt").ToList();
        diff.Should().Contain(l => l.Contains("old.txt"));
        diff.Should().Contain(l => l.Contains("new.txt"));
    }

    [Fact]
    public void SequenceMatcher_GetGroupedOpcodes_GroupsByContext()
    {
        var a = Enumerable.Range(1, 40).Select(i => $"line{i}\n").ToList();
        var b = new List<string>(a);
        b[8] = "changed\n";
        b[20] = "also changed\n";
        var sm = new SequenceMatcher<string>(null, a, b);
        var groups = sm.GetGroupedOpcodes(3);
        groups.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void SequenceMatcher_EmptyInput_GetOpcodes_IsEmpty()
    {
        var sm = new SequenceMatcher<string>(null,
            Array.Empty<string>(),
            Array.Empty<string>());
        sm.GetOpcodes().Should().BeEmpty();
    }
}
