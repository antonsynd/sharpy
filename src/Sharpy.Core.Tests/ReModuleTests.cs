using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class ReModuleTests
    {
        #region Search

        [Fact]
        public void Search_SimplePattern_FindsMatch()
        {
            var m = Re.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal("world", m!.Group());
        }

        [Fact]
        public void Search_NoMatch_ReturnsNull()
        {
            var m = Re.Search("xyz", "hello world");
            Assert.Null(m);
        }

        [Fact]
        public void Search_WithGroups_CapturesGroups()
        {
            var m = Re.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello world", m!.Group());
            Assert.Equal("hello", m.Group(1));
            Assert.Equal("world", m.Group(2));
        }

        [Fact]
        public void Search_StartEnd_MatchPosition()
        {
            var m = Re.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal(6, m!.Start());
            Assert.Equal(11, m.End());
        }

        [Fact]
        public void Search_Span_ReturnsTuple()
        {
            var m = Re.Search("world", "hello world");
            Assert.NotNull(m);
            var span = m!.Span();
            Assert.Equal((6, 11), span);
        }

        #endregion

        #region Match

        [Fact]
        public void Match_AtStart_Matches()
        {
            var m = Re.Match("hello", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello", m!.Group());
        }

        [Fact]
        public void Match_NotAtStart_ReturnsNull()
        {
            var m = Re.Match("world", "hello world");
            Assert.Null(m);
        }

        #endregion

        #region Fullmatch

        [Fact]
        public void Fullmatch_EntireString_Matches()
        {
            var m = Re.Fullmatch(@"\d+", "12345");
            Assert.NotNull(m);
            Assert.Equal("12345", m!.Group());
        }

        [Fact]
        public void Fullmatch_PartialString_ReturnsNull()
        {
            var m = Re.Fullmatch(@"\d+", "123abc");
            Assert.Null(m);
        }

        #endregion

        #region Findall

        [Fact]
        public void Findall_MultipleMatches_ReturnsAll()
        {
            var result = Re.Findall(@"\d+", "abc 123 def 456");
            Assert.Equal(2, ((ICollection<object?>)result).Count);
            Assert.Equal("123", result[0]);
            Assert.Equal("456", result[1]);
        }

        [Fact]
        public void Findall_NoMatch_ReturnsEmptyList()
        {
            var result = Re.Findall(@"\d+", "abcdef");
            Assert.Empty(result);
        }

        [Fact]
        public void Findall_WithSingleGroup_ReturnsGroupValues()
        {
            var result = Re.Findall(@"(\w+)@(\w+)", "a@b c@d");
            // Multiple groups → returns list of lists
            Assert.Equal(2, ((ICollection<object?>)result).Count);
        }

        #endregion

        #region Finditer

        [Fact]
        public void Finditer_MultipleMatches_ReturnsMatchObjects()
        {
            var result = Re.Finditer(@"\d+", "abc 123 def 456");
            Assert.Equal(2, ((ICollection<ReMatch>)result).Count);
            Assert.Equal("123", result[0].Group());
            Assert.Equal("456", result[1].Group());
        }

        #endregion

        #region Sub

        [Fact]
        public void Sub_ReplaceAll_ReplacesAllOccurrences()
        {
            string result = Re.Sub(@"\d+", "NUM", "abc 123 def 456");
            Assert.Equal("abc NUM def NUM", result);
        }

        [Fact]
        public void Sub_WithCount_ReplacesLimited()
        {
            string result = Re.Sub(@"\d+", "NUM", "abc 123 def 456", count: 1);
            Assert.Equal("abc NUM def 456", result);
        }

        [Fact]
        public void Sub_BackReference_Works()
        {
            string result = Re.Sub(@"(\w+)\s(\w+)", "$2 $1", "hello world");
            Assert.Equal("world hello", result);
        }

        #endregion

        #region Split

        [Fact]
        public void Split_SimplePattern_SplitsString()
        {
            var result = Re.Split(@"\s+", "one two  three");
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("one", result[0]);
            Assert.Equal("two", result[1]);
            Assert.Equal("three", result[2]);
        }

        [Fact]
        public void Split_WithMaxsplit_LimitsResults()
        {
            var result = Re.Split(@"\s+", "one two three four", maxsplit: 2);
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("one", result[0]);
            Assert.Equal("two", result[1]);
            Assert.Equal("three four", result[2]);
        }

        #endregion

        #region Compile

        [Fact]
        public void Compile_ReturnPattern_CanBeReused()
        {
            var pattern = Re.Compile(@"\d+");
            var m1 = pattern.Search("abc 123");
            var m2 = pattern.Search("def 456");
            Assert.NotNull(m1);
            Assert.NotNull(m2);
            Assert.Equal("123", m1!.Group());
            Assert.Equal("456", m2!.Group());
        }

        [Fact]
        public void Compile_PatternStr_ReturnsOriginal()
        {
            var pattern = Re.Compile(@"\d+");
            Assert.Equal(@"\d+", pattern.PatternStr);
        }

        #endregion

        #region Flags

        [Fact]
        public void Search_IgnoreCase_MatchesCaseInsensitive()
        {
            var m = Re.Search("hello", "HELLO WORLD", flags: Re.IGNORECASE);
            Assert.NotNull(m);
            Assert.Equal("HELLO", m!.Group());
        }

        [Fact]
        public void Search_IgnoreCaseShorthand_Works()
        {
            var m = Re.Search("hello", "HELLO WORLD", flags: Re.I);
            Assert.NotNull(m);
        }

        [Fact]
        public void Search_Multiline_MatchesAtLineStart()
        {
            var m = Re.Search("^world", "hello\nworld", flags: Re.MULTILINE);
            Assert.NotNull(m);
            Assert.Equal("world", m!.Group());
        }

        [Fact]
        public void Search_Dotall_DotMatchesNewline()
        {
            var m = Re.Fullmatch("a.b", "a\nb", flags: Re.DOTALL);
            Assert.NotNull(m);
        }

        [Fact]
        public void Search_CombinedFlags_Work()
        {
            var m = Re.Search("^hello", "HELLO\nWORLD", flags: Re.IGNORECASE | Re.MULTILINE);
            Assert.NotNull(m);
            Assert.Equal("HELLO", m!.Group());
        }

        #endregion

        #region Named Groups

        [Fact]
        public void Search_NamedGroup_PythonSyntax_Works()
        {
            // (?P<name>...) Python syntax → translated to (?<name>...) .NET syntax
            var m = Re.Search(@"(?P<first>\w+)\s(?P<last>\w+)", "John Smith");
            Assert.NotNull(m);
            Assert.Equal("John", m!.Group("first"));
            Assert.Equal("Smith", m!.Group("last"));
        }

        [Fact]
        public void Search_NamedGroup_Groupdict_Works()
        {
            var m = Re.Search(@"(?P<first>\w+)\s(?P<last>\w+)", "John Smith");
            Assert.NotNull(m);
            var gd = m!.Groupdict();
            Assert.Equal("John", gd["first"]);
            Assert.Equal("Smith", gd["last"]);
        }

        [Fact]
        public void Search_NamedBackref_PythonSyntax_Works()
        {
            // (?P=name) Python syntax → translated to \k<name> .NET syntax
            var m = Re.Search(@"(?P<word>\w+)\s(?P=word)", "hello hello");
            Assert.NotNull(m);
            Assert.Equal("hello hello", m!.Group());
        }

        [Fact]
        public void Search_DotNetNamedGroup_AlsoWorks()
        {
            // .NET native syntax should also work
            var m = Re.Search(@"(?<first>\w+)\s(?<last>\w+)", "John Smith");
            Assert.NotNull(m);
            Assert.Equal("John", m!.Group("first"));
        }

        #endregion

        #region Groups

        [Fact]
        public void Groups_ReturnsAllSubgroups()
        {
            var m = Re.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            var groups = m!.Groups();
            Assert.Equal(2, ((ICollection<string?>)groups).Count);
            Assert.Equal("hello", groups[0]);
            Assert.Equal("world", groups[1]);
        }

        [Fact]
        public void Groups_NonParticipating_ReturnsNull()
        {
            var m = Re.Search(@"(\w+)|(\d+)", "hello");
            Assert.NotNull(m);
            var groups = m!.Groups();
            Assert.Equal("hello", groups[0]);
            Assert.Null(groups[1]);
        }

        #endregion

        #region Escape

        [Fact]
        public void Escape_SpecialChars_AreEscaped()
        {
            string result = Re.Escape("hello.world*");
            // Should be able to use the escaped pattern literally
            var m = Re.Search(result, "hello.world*");
            Assert.NotNull(m);
        }

        [Fact]
        public void Escape_PlainText_Unchanged()
        {
            string result = Re.Escape("hello");
            Assert.Equal("hello", result);
        }

        #endregion

        #region Match Properties

        [Fact]
        public void Match_String_ReturnsInput()
        {
            var m = Re.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello world", m!.String);
        }

        [Fact]
        public void Match_Pattern_ReturnsPattern()
        {
            var m = Re.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal("world", m!.Pattern);
        }

        [Fact]
        public void Match_ToString_ReturnsReadableFormat()
        {
            var m = Re.Search("world", "hello world");
            Assert.NotNull(m);
            string s = m!.ToString();
            Assert.Contains("span=(6, 11)", s);
            Assert.Contains("match='world'", s);
        }

        #endregion

        #region Pattern Translation (verified via public API)

        [Fact]
        public void PythonNamedGroup_Translated_MatchesCorrectly()
        {
            // (?P<name>...) syntax should work via translation
            var m = Re.Search(@"(?P<num>\d+)", "abc 123");
            Assert.NotNull(m);
            Assert.Equal("123", m!.Group("num"));
        }

        [Fact]
        public void PythonNamedBackref_Translated_MatchesCorrectly()
        {
            // (?P=name) syntax should work via translation
            var m = Re.Search(@"(?P<w>\w+)\s(?P=w)", "abc abc");
            Assert.NotNull(m);
            Assert.Equal("abc abc", m!.Group());
        }

        [Fact]
        public void NoSpecialSyntax_WorksUnchanged()
        {
            var m = Re.Search(@"\d+", "abc 123");
            Assert.NotNull(m);
            Assert.Equal("123", m!.Group());
        }

        #endregion
    }
}
