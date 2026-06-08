using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    /// <summary>
    /// Tests for RePattern compiled object instance methods and ReMatch position-based access.
    /// These complement ReModuleTests.cs which tests the static ReModule.* API.
    /// </summary>
    public class RePatternTests
    {
        #region RePattern Instance - Match

        [Fact]
        public void Pattern_Match_AtStart_ReturnsMatch()
        {
            var pattern = ReModule.Compile(@"\d+");
            var m = pattern.Match("123abc");
            Assert.NotNull(m);
            Assert.Equal("123", m!.Group());
        }

        [Fact]
        public void Pattern_Match_NotAtStart_ReturnsNull()
        {
            var pattern = ReModule.Compile(@"\d+");
            var m = pattern.Match("abc123");
            Assert.Null(m);
        }

        [Fact]
        public void Pattern_Match_WithPos_MatchesFromPosition()
        {
            var pattern = ReModule.Compile(@"\d+");
            // pos=3 shifts start so digits start at index 3
            var m = pattern.Match("abc123", pos: 3);
            Assert.NotNull(m);
            Assert.Equal("123", m!.Group());
        }

        [Fact]
        public void Pattern_Match_EmptyString_EmptyPatternMatches()
        {
            var pattern = ReModule.Compile(@".*");
            var m = pattern.Match("");
            Assert.NotNull(m);
            Assert.Equal("", m!.Group());
        }

        #endregion

        #region RePattern Instance - Fullmatch

        [Fact]
        public void Pattern_Fullmatch_EntireString_ReturnsMatch()
        {
            var pattern = ReModule.Compile(@"[a-z]+");
            var m = pattern.Fullmatch("hello");
            Assert.NotNull(m);
            Assert.Equal("hello", m!.Group());
        }

        [Fact]
        public void Pattern_Fullmatch_PartialString_ReturnsNull()
        {
            var pattern = ReModule.Compile(@"[a-z]+");
            var m = pattern.Fullmatch("hello123");
            Assert.Null(m);
        }

        [Fact]
        public void Pattern_Fullmatch_WithGroups_CapturesAllGroups()
        {
            var pattern = ReModule.Compile(@"(\d{4})-(\d{2})-(\d{2})");
            var m = pattern.Fullmatch("2024-01-15");
            Assert.NotNull(m);
            Assert.Equal("2024", m!.Group(1));
            Assert.Equal("01", m.Group(2));
            Assert.Equal("15", m.Group(3));
        }

        #endregion

        #region RePattern Instance - Findall

        [Fact]
        public void Pattern_Findall_MultipleMatches_ReturnsAll()
        {
            var pattern = ReModule.Compile(@"\d+");
            var result = pattern.Findall("a1b22c333");
            Assert.Equal(3, ((ICollection<object>)result).Count);
            Assert.Equal("1", result[0]);
            Assert.Equal("22", result[1]);
            Assert.Equal("333", result[2]);
        }

        [Fact]
        public void Pattern_Findall_NoMatches_ReturnsEmptyList()
        {
            var pattern = ReModule.Compile(@"\d+");
            var result = pattern.Findall("abcdef");
            Assert.Empty(result);
        }

        [Fact]
        public void Pattern_Findall_SingleGroup_ReturnsGroupValues()
        {
            var pattern = ReModule.Compile(@"(\d+)");
            var result = pattern.Findall("a1b2c3");
            Assert.Equal(3, ((ICollection<object>)result).Count);
            Assert.Equal("1", result[0]);
            Assert.Equal("2", result[1]);
            Assert.Equal("3", result[2]);
        }

        #endregion

        #region RePattern Instance - Finditer

        [Fact]
        public void Pattern_Finditer_ReturnsMatchObjects()
        {
            var pattern = ReModule.Compile(@"\w+");
            var matches = pattern.Finditer("hello world");
            Assert.Equal(2, ((ICollection<ReModule.Match>)matches).Count);
            Assert.Equal("hello", matches[0].Group());
            Assert.Equal("world", matches[1].Group());
        }

        [Fact]
        public void Pattern_Finditer_PositionsAreCorrect()
        {
            var pattern = ReModule.Compile(@"\d+");
            var matches = pattern.Finditer("abc 123 def 456");
            Assert.Equal(2, ((ICollection<ReModule.Match>)matches).Count);
            Assert.Equal(4, matches[0].Start());
            Assert.Equal(7, matches[0].End());
            Assert.Equal(12, matches[1].Start());
            Assert.Equal(15, matches[1].End());
        }

        #endregion

        #region RePattern Instance - Sub

        [Fact]
        public void Pattern_Sub_ReplacesAll()
        {
            var pattern = ReModule.Compile(@"\d+");
            string result = pattern.Sub("NUM", "a1b2c3");
            Assert.Equal("aNUMbNUMcNUM", result);
        }

        [Fact]
        public void Pattern_Sub_WithCount_ReplacesLimited()
        {
            var pattern = ReModule.Compile(@"\d+");
            string result = pattern.Sub("NUM", "a1b2c3", count: 2);
            Assert.Equal("aNUMbNUMc3", result);
        }

        [Fact]
        public void Pattern_Sub_Count0_ReplacesAll()
        {
            var pattern = ReModule.Compile(@"\d+");
            string result = pattern.Sub("X", "1a2b3c", count: 0);
            Assert.Equal("XaXbXc", result);
        }

        #endregion

        #region RePattern Instance - Split

        [Fact]
        public void Pattern_Split_SplitsOnPattern()
        {
            var pattern = ReModule.Compile(@"\s+");
            var result = pattern.Split("one two   three");
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("one", result[0]);
            Assert.Equal("two", result[1]);
            Assert.Equal("three", result[2]);
        }

        [Fact]
        public void Pattern_Split_WithMaxsplit_LimitsResults()
        {
            var pattern = ReModule.Compile(@",");
            var result = pattern.Split("a,b,c,d", maxsplit: 2);
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("b", result[1]);
            Assert.Equal("c,d", result[2]);
        }

        #endregion

        #region RePattern Properties

        [Fact]
        public void Pattern_Flags_ReflectsCompileFlags()
        {
            var pattern = ReModule.Compile(@"\d+", flags: ReModule.IGNORECASE);
            Assert.Equal(ReModule.IGNORECASE, pattern.Flags);
        }

        [Fact]
        public void Pattern_Flags_Zero_WhenNoFlags()
        {
            var pattern = ReModule.Compile(@"\d+");
            Assert.Equal(0, pattern.Flags);
        }

        [Fact]
        public void Pattern_ToString_ContainsPatternStr()
        {
            var pattern = ReModule.Compile(@"\d+");
            string s = pattern.ToString();
            Assert.Contains(@"\d+", s, StringComparison.Ordinal);
        }

        [Fact]
        public void Pattern_Compile_CombinedFlags_Work()
        {
            var pattern = ReModule.Compile("^hello", flags: ReModule.IGNORECASE | ReModule.MULTILINE);
            var m = pattern.Search("HELLO\nWORLD");
            Assert.NotNull(m);
            Assert.Equal("HELLO", m!.Group());
        }

        [Fact]
        public void Pattern_Compile_Dotall_DotMatchesNewline()
        {
            var pattern = ReModule.Compile("a.b", flags: ReModule.DOTALL);
            var m = pattern.Fullmatch("a\nb");
            Assert.NotNull(m);
        }

        #endregion

        #region ReMatch Group - Out-Of-Range and Edge Cases

        [Fact]
        public void Match_Group_OutOfRange_ThrowsIndexError()
        {
            var m = ReModule.Compile(@"(\d+)").Match("123");
            Assert.NotNull(m);
            Assert.Throws<IndexError>(() => m!.Group(99));
        }

        [Fact]
        public void Match_Group_NegativeIndex_ThrowsIndexError()
        {
            var m = ReModule.Compile(@"(\d+)").Match("123");
            Assert.NotNull(m);
            Assert.Throws<IndexError>(() => m!.Group(-1));
        }

        [Fact]
        public void Match_Group_ZeroIsFullMatch()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello world", m!.Group(0));
        }

        #endregion

        #region ReMatch Start/End/Span with group argument

        [Fact]
        public void Match_Start_WithGroup_ReturnsGroupStart()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal(6, m!.Start(2));
        }

        [Fact]
        public void Match_End_WithGroup_ReturnsGroupEnd()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal(11, m!.End(2));
        }

        [Fact]
        public void Match_Span_WithGroup_ReturnsTuple()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal((6, 11), m!.Span(2));
        }

        [Fact]
        public void Match_Start_DefaultGroupZero_IsFullMatchStart()
        {
            var m = ReModule.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal(6, m!.Start(0));
        }

        #endregion

        #region Named Groups via Compiled Pattern

        [Fact]
        public void Pattern_NamedGroup_MatchReturnsNamedGroup()
        {
            var pattern = ReModule.Compile(@"(?P<year>\d{4})-(?P<month>\d{2})-(?P<day>\d{2})");
            var m = pattern.Fullmatch("2024-03-15");
            Assert.NotNull(m);
            Assert.Equal("2024", m!.Group("year"));
            Assert.Equal("03", m.Group("month"));
            Assert.Equal("15", m.Group("day"));
        }

        [Fact]
        public void Pattern_Groupdict_ReturnsNamedGroupDict()
        {
            var pattern = ReModule.Compile(@"(?P<first>\w+)\s(?P<last>\w+)");
            var m = pattern.Search("Jane Doe");
            Assert.NotNull(m);
            var gd = m!.Groupdict();
            Assert.Equal("Jane", gd["first"]);
            Assert.Equal("Doe", gd["last"]);
        }

        #endregion

        #region Empty String Edge Cases

        [Fact]
        public void Pattern_Search_EmptyString_EmptyPatternMatches()
        {
            var pattern = ReModule.Compile(@"");
            var m = pattern.Search("");
            Assert.NotNull(m);
            Assert.Equal("", m!.Group());
        }

        [Fact]
        public void Pattern_Search_EmptyString_NonEmptyPattern_ReturnsNull()
        {
            var pattern = ReModule.Compile(@"\d+");
            var m = pattern.Search("");
            Assert.Null(m);
        }

        [Fact]
        public void Pattern_Match_NoMatch_ReturnsNullNotException()
        {
            var pattern = ReModule.Compile(@"\d{10}");
            var m = pattern.Match("abc");
            Assert.Null(m);
        }

        #endregion

        #region ReMatch.Re Property

        [Fact]
        public void Match_Re_ReturnsCompiledPattern()
        {
            var pattern = ReModule.Compile(@"\d+");
            var m = pattern.Search("abc 123");
            Assert.NotNull(m);
            Assert.Same(pattern, m!.Re);
        }

        [Fact]
        public void Match_Re_FromStaticApi_IsNotNull()
        {
            var m = ReModule.Search(@"\d+", "abc 123");
            Assert.NotNull(m);
            Assert.NotNull(m!.Re);
        }

        #endregion

        #region Lastindex / Lastgroup

        [Fact]
        public void Match_Lastindex_ReturnsLastMatchedGroupIndex()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal(2, m!.Lastindex);
        }

        [Fact]
        public void Match_Lastindex_NoGroups_ReturnsNull()
        {
            var m = ReModule.Search(@"\w+", "hello");
            Assert.NotNull(m);
            Assert.Null(m!.Lastindex);
        }

        [Fact]
        public void Match_Lastindex_FirstAlternative_ReturnsCorrectIndex()
        {
            // Only the first alternative matches → group 1 is the last matched
            var m = ReModule.Search(@"(\w+)|(\d+)", "hello");
            Assert.NotNull(m);
            Assert.Equal(1, m!.Lastindex);
        }

        [Fact]
        public void Match_Lastgroup_NamedGroup_ReturnsName()
        {
            var pattern = ReModule.Compile(@"(?P<first>\w+)\s(?P<last>\w+)");
            var m = pattern.Search("hello world");
            Assert.NotNull(m);
            Assert.Equal("last", m!.Lastgroup);
        }

        [Fact]
        public void Match_Lastgroup_UnnamedGroup_ReturnsNull()
        {
            var pattern = ReModule.Compile(@"(\w+)\s(\w+)");
            var m = pattern.Search("hello world");
            Assert.NotNull(m);
            Assert.Null(m!.Lastgroup);
        }

        [Fact]
        public void Match_Lastgroup_NoGroups_ReturnsNull()
        {
            var pattern = ReModule.Compile(@"\w+");
            var m = pattern.Search("hello");
            Assert.NotNull(m);
            Assert.Null(m!.Lastgroup);
        }

        #endregion

        #region Expand

        [Fact]
        public void Match_Expand_BackslashDigit_ExpandsGroup()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            string result = m!.Expand(@"\2 \1");
            Assert.Equal("world hello", result);
        }

        [Fact]
        public void Match_Expand_BackslashG_ExpandsNamedGroup()
        {
            var pattern = ReModule.Compile(@"(?P<first>\w+)\s(?P<last>\w+)");
            var m = pattern.Search("hello world");
            Assert.NotNull(m);
            string result = m!.Expand(@"\g<last> \g<first>");
            Assert.Equal("world hello", result);
        }

        [Fact]
        public void Match_Expand_BackslashG_NumericReference()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            string result = m!.Expand(@"\g<2> \g<1>");
            Assert.Equal("world hello", result);
        }

        [Fact]
        public void Match_Expand_PlainText_PassedThrough()
        {
            var m = ReModule.Search(@"\w+", "hello");
            Assert.NotNull(m);
            string result = m!.Expand("result: \\0");
            Assert.Equal("result: hello", result);
        }

        #endregion

        #region Indexer

        [Fact]
        public void Match_Indexer_ZeroReturnsFullMatch()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello world", m![0]);
        }

        [Fact]
        public void Match_Indexer_GroupReturnsGroupValue()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello", m![1]);
            Assert.Equal("world", m![2]);
        }

        [Fact]
        public void Match_Indexer_OutOfRange_ThrowsIndexError()
        {
            var m = ReModule.Search(@"\w+", "hello");
            Assert.NotNull(m);
            Assert.Throws<IndexError>(() => m![99]);
        }

        [Fact]
        public void Match_GroupByName_NonexistentName_ThrowsIndexError()
        {
            var m = ReModule.Search(@"(?P<word>\w+)", "hello");
            Assert.NotNull(m);
            Assert.Throws<IndexError>(() => m!.Group("nonexistent"));
        }

        #endregion

        #region RePattern.PatternStr Property

        [Fact]
        public void Pattern_PatternStrProperty_ReturnsOriginal()
        {
            var pattern = ReModule.Compile(@"\d+");
            Assert.Equal(@"\d+", pattern.PatternStr);
        }

        #endregion

        #region RePattern.Groups Count

        [Fact]
        public void Pattern_Groups_ReturnsGroupCount()
        {
            var pattern = ReModule.Compile(@"(\w+)\s(\w+)");
            Assert.Equal(2, pattern.Groups);
        }

        [Fact]
        public void Pattern_Groups_NoGroups_ReturnsZero()
        {
            var pattern = ReModule.Compile(@"\w+");
            Assert.Equal(0, pattern.Groups);
        }

        [Fact]
        public void Pattern_Groups_NamedGroups_CountedCorrectly()
        {
            var pattern = ReModule.Compile(@"(?P<first>\w+)\s(?P<last>\w+)");
            Assert.Equal(2, pattern.Groups);
        }

        #endregion

        #region RePattern.Groupindex

        [Fact]
        public void Pattern_Groupindex_ReturnsNameToNumberMapping()
        {
            var pattern = ReModule.Compile(@"(?P<first>\w+)\s(?P<last>\w+)");
            var gi = pattern.Groupindex;
            Assert.Equal(1, gi["first"]);
            Assert.Equal(2, gi["last"]);
        }

        [Fact]
        public void Pattern_Groupindex_NoNamedGroups_ReturnsEmptyDict()
        {
            var pattern = ReModule.Compile(@"(\w+)\s(\w+)");
            var gi = pattern.Groupindex;
            Assert.Empty(gi);
        }

        [Fact]
        public void Pattern_Groupindex_MixedNamedUnnamed_OnlyNamed()
        {
            var pattern = ReModule.Compile(@"(?P<name>\w+)\s(\d+)");
            var gi = pattern.Groupindex;
            Assert.Single((ICollection<KeyValuePair<string, int>>)gi);
            // .NET numbers unnamed groups first, then named groups
            Assert.Equal(2, gi["name"]);
        }

        #endregion

        #region RePattern.Subn

        [Fact]
        public void Pattern_Subn_ReturnsStringAndCount()
        {
            var pattern = ReModule.Compile(@"\d+");
            var (result, count) = pattern.Subn("NUM", "a1b2c3");
            Assert.Equal("aNUMbNUMcNUM", result);
            Assert.Equal(3, count);
        }

        [Fact]
        public void Pattern_Subn_WithCount_LimitsReplacements()
        {
            var pattern = ReModule.Compile(@"\d+");
            var (result, count) = pattern.Subn("NUM", "a1b2c3", count: 2);
            Assert.Equal("aNUMbNUMc3", result);
            Assert.Equal(2, count);
        }

        [Fact]
        public void Pattern_Subn_NoMatch_ReturnsOriginalAndZero()
        {
            var pattern = ReModule.Compile(@"\d+");
            var (result, count) = pattern.Subn("NUM", "no numbers");
            Assert.Equal("no numbers", result);
            Assert.Equal(0, count);
        }

        #endregion

        #region RePattern Callable Sub/Subn

        [Fact]
        public void Pattern_Sub_Callable_Works()
        {
            var pattern = ReModule.Compile(@"\d+");
            string result = pattern.Sub(m => "[" + m.Group() + "]", "a1b2c3");
            Assert.Equal("a[1]b[2]c[3]", result);
        }

        [Fact]
        public void Pattern_Sub_Callable_WithCount_LimitsReplacements()
        {
            var pattern = ReModule.Compile(@"\d+");
            string result = pattern.Sub(m => "X", "a1b2c3", count: 1);
            Assert.Equal("aXb2c3", result);
        }

        [Fact]
        public void Pattern_Subn_Callable_Works()
        {
            var pattern = ReModule.Compile(@"\d+");
            var (result, count) = pattern.Subn(m => "X", "a1b2c3");
            Assert.Equal("aXbXcX", result);
            Assert.Equal(3, count);
        }

        [Fact]
        public void Pattern_Subn_Callable_WithCount_LimitsReplacements()
        {
            var pattern = ReModule.Compile(@"\d+");
            var (result, count) = pattern.Subn(m => "X", "a1b2c3", count: 2);
            Assert.Equal("aXbXc3", result);
            Assert.Equal(2, count);
        }

        #endregion

        #region ReModule.Error

        [Fact]
        public void Compile_InvalidPattern_ThrowsReError()
        {
            Assert.Throws<ReModule.Error>(() => ReModule.Compile("[invalid"));
        }

        [Fact]
        public void ReError_HasMessageProperty()
        {
            var ex = Assert.Throws<ReModule.Error>(() => ReModule.Compile("[invalid"));
            Assert.NotEmpty(ex.Msg);
            Assert.Equal("[invalid", ex.Pattern);
        }

        [Fact]
        public void ReError_Properties_SetCorrectly()
        {
            var err = new ReModule.Error("test error", "abc(", 3);
            Assert.Equal("test error", err.Msg);
            Assert.Equal("abc(", err.Pattern);
            Assert.Equal(3, err.Pos);
            Assert.NotNull(err.Lineno);
            Assert.NotNull(err.Colno);
        }

        [Fact]
        public void ReError_NullPattern_NullPos()
        {
            var err = new ReModule.Error("test error");
            Assert.Equal("test error", err.Msg);
            Assert.Null(err.Pattern);
            Assert.Null(err.Pos);
            Assert.Null(err.Lineno);
            Assert.Null(err.Colno);
        }

        #endregion

        #region Inline Flags

        [Fact]
        public void InlineFlag_CaseInsensitive_Works()
        {
            var m = ReModule.Search(@"(?i)hello", "HELLO world");
            Assert.NotNull(m);
            Assert.Equal("HELLO", m!.Group());
        }

        [Fact]
        public void InlineFlag_AsciiStripped_Works()
        {
            // (?a) should be stripped (no-op on .NET), pattern should still match
            var m = ReModule.Search(@"(?a)\w+", "hello");
            Assert.NotNull(m);
            Assert.Equal("hello", m!.Group());
        }

        [Fact]
        public void InlineFlag_UnicodeStripped_Works()
        {
            // (?u) should be stripped (no-op on .NET)
            var m = ReModule.Search(@"(?u)\w+", "hello");
            Assert.NotNull(m);
            Assert.Equal("hello", m!.Group());
        }

        [Fact]
        public void InlineFlag_ScopedWithColon_Works()
        {
            // (?i:hello) should apply case-insensitive to the group
            var m = ReModule.Search(@"(?i:hello) world", "HELLO world");
            Assert.NotNull(m);
            Assert.Equal("HELLO world", m!.Group());
        }

        [Fact]
        public void InlineFlag_ScopedAsciiStripped_Works()
        {
            // (?a:hello) — strip 'a' flag, emit as (?:hello)
            var m = ReModule.Search(@"(?a:hello) world", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello world", m!.Group());
        }

        [Fact]
        public void InlineFlag_MixedFlags_StripsOnlyPythonOnly()
        {
            // (?ai) — strip 'a', keep 'i'
            var m = ReModule.Search(@"(?ai)hello", "HELLO");
            Assert.NotNull(m);
            Assert.Equal("HELLO", m!.Group());
        }

        #endregion
    }
}
