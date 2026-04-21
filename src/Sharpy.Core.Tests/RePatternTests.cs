using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    /// <summary>
    /// Tests for RePattern compiled object instance methods and ReMatch position-based access.
    /// These complement ReModuleTests.cs which tests the static Re.* API.
    /// </summary>
    public class RePatternTests
    {
        #region RePattern Instance - Match

        [Fact]
        public void Pattern_Match_AtStart_ReturnsMatch()
        {
            var pattern = Re.Compile(@"\d+");
            var m = pattern.Match("123abc");
            Assert.NotNull(m);
            Assert.Equal("123", m!.Group());
        }

        [Fact]
        public void Pattern_Match_NotAtStart_ReturnsNull()
        {
            var pattern = Re.Compile(@"\d+");
            var m = pattern.Match("abc123");
            Assert.Null(m);
        }

        [Fact]
        public void Pattern_Match_WithPos_MatchesFromPosition()
        {
            var pattern = Re.Compile(@"\d+");
            // pos=3 shifts start so digits start at index 3
            var m = pattern.Match("abc123", pos: 3);
            Assert.NotNull(m);
            Assert.Equal("123", m!.Group());
        }

        [Fact]
        public void Pattern_Match_EmptyString_EmptyPatternMatches()
        {
            var pattern = Re.Compile(@".*");
            var m = pattern.Match("");
            Assert.NotNull(m);
            Assert.Equal("", m!.Group());
        }

        #endregion

        #region RePattern Instance - Fullmatch

        [Fact]
        public void Pattern_Fullmatch_EntireString_ReturnsMatch()
        {
            var pattern = Re.Compile(@"[a-z]+");
            var m = pattern.Fullmatch("hello");
            Assert.NotNull(m);
            Assert.Equal("hello", m!.Group());
        }

        [Fact]
        public void Pattern_Fullmatch_PartialString_ReturnsNull()
        {
            var pattern = Re.Compile(@"[a-z]+");
            var m = pattern.Fullmatch("hello123");
            Assert.Null(m);
        }

        [Fact]
        public void Pattern_Fullmatch_WithGroups_CapturesAllGroups()
        {
            var pattern = Re.Compile(@"(\d{4})-(\d{2})-(\d{2})");
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
            var pattern = Re.Compile(@"\d+");
            var result = pattern.Findall("a1b22c333");
            Assert.Equal(3, ((ICollection<object?>)result).Count);
            Assert.Equal("1", result[0]);
            Assert.Equal("22", result[1]);
            Assert.Equal("333", result[2]);
        }

        [Fact]
        public void Pattern_Findall_NoMatches_ReturnsEmptyList()
        {
            var pattern = Re.Compile(@"\d+");
            var result = pattern.Findall("abcdef");
            Assert.Empty(result);
        }

        [Fact]
        public void Pattern_Findall_SingleGroup_ReturnsGroupValues()
        {
            var pattern = Re.Compile(@"(\d+)");
            var result = pattern.Findall("a1b2c3");
            Assert.Equal(3, ((ICollection<object?>)result).Count);
            Assert.Equal("1", result[0]);
            Assert.Equal("2", result[1]);
            Assert.Equal("3", result[2]);
        }

        #endregion

        #region RePattern Instance - Finditer

        [Fact]
        public void Pattern_Finditer_ReturnsMatchObjects()
        {
            var pattern = Re.Compile(@"\w+");
            var matches = pattern.Finditer("hello world");
            Assert.Equal(2, ((ICollection<ReMatch>)matches).Count);
            Assert.Equal("hello", matches[0].Group());
            Assert.Equal("world", matches[1].Group());
        }

        [Fact]
        public void Pattern_Finditer_PositionsAreCorrect()
        {
            var pattern = Re.Compile(@"\d+");
            var matches = pattern.Finditer("abc 123 def 456");
            Assert.Equal(2, ((ICollection<ReMatch>)matches).Count);
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
            var pattern = Re.Compile(@"\d+");
            string result = pattern.Sub("NUM", "a1b2c3");
            Assert.Equal("aNUMbNUMcNUM", result);
        }

        [Fact]
        public void Pattern_Sub_WithCount_ReplacesLimited()
        {
            var pattern = Re.Compile(@"\d+");
            string result = pattern.Sub("NUM", "a1b2c3", count: 2);
            Assert.Equal("aNUMbNUMc3", result);
        }

        [Fact]
        public void Pattern_Sub_Count0_ReplacesAll()
        {
            var pattern = Re.Compile(@"\d+");
            string result = pattern.Sub("X", "1a2b3c", count: 0);
            Assert.Equal("XaXbXc", result);
        }

        #endregion

        #region RePattern Instance - Split

        [Fact]
        public void Pattern_Split_SplitsOnPattern()
        {
            var pattern = Re.Compile(@"\s+");
            var result = pattern.Split("one two   three");
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("one", result[0]);
            Assert.Equal("two", result[1]);
            Assert.Equal("three", result[2]);
        }

        [Fact]
        public void Pattern_Split_WithMaxsplit_LimitsResults()
        {
            var pattern = Re.Compile(@",");
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
            var pattern = Re.Compile(@"\d+", flags: Re.IGNORECASE);
            Assert.Equal(Re.IGNORECASE, pattern.Flags);
        }

        [Fact]
        public void Pattern_Flags_Zero_WhenNoFlags()
        {
            var pattern = Re.Compile(@"\d+");
            Assert.Equal(0, pattern.Flags);
        }

        [Fact]
        public void Pattern_ToString_ContainsPatternStr()
        {
            var pattern = Re.Compile(@"\d+");
            string s = pattern.ToString();
            Assert.Contains(@"\d+", s, StringComparison.Ordinal);
        }

        [Fact]
        public void Pattern_Compile_CombinedFlags_Work()
        {
            var pattern = Re.Compile("^hello", flags: Re.IGNORECASE | Re.MULTILINE);
            var m = pattern.Search("HELLO\nWORLD");
            Assert.NotNull(m);
            Assert.Equal("HELLO", m!.Group());
        }

        [Fact]
        public void Pattern_Compile_Dotall_DotMatchesNewline()
        {
            var pattern = Re.Compile("a.b", flags: Re.DOTALL);
            var m = pattern.Fullmatch("a\nb");
            Assert.NotNull(m);
        }

        #endregion

        #region ReMatch Group - Out-Of-Range and Edge Cases

        [Fact]
        public void Match_Group_OutOfRange_ThrowsIndexError()
        {
            var m = Re.Match(@"(\d+)", "123");
            Assert.NotNull(m);
            Assert.Throws<IndexError>(() => m!.Group(99));
        }

        [Fact]
        public void Match_Group_NegativeIndex_ThrowsIndexError()
        {
            var m = Re.Match(@"(\d+)", "123");
            Assert.NotNull(m);
            Assert.Throws<IndexError>(() => m!.Group(-1));
        }

        [Fact]
        public void Match_Group_ZeroIsFullMatch()
        {
            var m = Re.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello world", m!.Group(0));
        }

        #endregion

        #region ReMatch Start/End/Span with group argument

        [Fact]
        public void Match_Start_WithGroup_ReturnsGroupStart()
        {
            var m = Re.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal(6, m!.Start(2));
        }

        [Fact]
        public void Match_End_WithGroup_ReturnsGroupEnd()
        {
            var m = Re.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal(11, m!.End(2));
        }

        [Fact]
        public void Match_Span_WithGroup_ReturnsTuple()
        {
            var m = Re.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal((6, 11), m!.Span(2));
        }

        [Fact]
        public void Match_Start_DefaultGroupZero_IsFullMatchStart()
        {
            var m = Re.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal(6, m!.Start(0));
        }

        #endregion

        #region Named Groups via Compiled Pattern

        [Fact]
        public void Pattern_NamedGroup_MatchReturnsNamedGroup()
        {
            var pattern = Re.Compile(@"(?P<year>\d{4})-(?P<month>\d{2})-(?P<day>\d{2})");
            var m = pattern.Fullmatch("2024-03-15");
            Assert.NotNull(m);
            Assert.Equal("2024", m!.Group("year"));
            Assert.Equal("03", m.Group("month"));
            Assert.Equal("15", m.Group("day"));
        }

        [Fact]
        public void Pattern_Groupdict_ReturnsNamedGroupDict()
        {
            var pattern = Re.Compile(@"(?P<first>\w+)\s(?P<last>\w+)");
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
            var pattern = Re.Compile(@"");
            var m = pattern.Search("");
            Assert.NotNull(m);
            Assert.Equal("", m!.Group());
        }

        [Fact]
        public void Pattern_Search_EmptyString_NonEmptyPattern_ReturnsNull()
        {
            var pattern = Re.Compile(@"\d+");
            var m = pattern.Search("");
            Assert.Null(m);
        }

        [Fact]
        public void Pattern_Match_NoMatch_ReturnsNullNotException()
        {
            var pattern = Re.Compile(@"\d{10}");
            var m = pattern.Match("abc");
            Assert.Null(m);
        }

        #endregion
    }
}
