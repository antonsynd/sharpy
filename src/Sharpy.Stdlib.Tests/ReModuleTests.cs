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
            var m = ReModule.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal("world", m!.Group());
        }

        [Fact]
        public void Search_NoMatch_ReturnsNull()
        {
            var m = ReModule.Search("xyz", "hello world");
            Assert.Null(m);
        }

        [Fact]
        public void Search_WithGroups_CapturesGroups()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello world", m!.Group());
            Assert.Equal("hello", m.Group(1));
            Assert.Equal("world", m.Group(2));
        }

        [Fact]
        public void Search_StartEnd_MatchPosition()
        {
            var m = ReModule.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal(6, m!.Start());
            Assert.Equal(11, m.End());
        }

        [Fact]
        public void Search_Span_ReturnsTuple()
        {
            var m = ReModule.Search("world", "hello world");
            Assert.NotNull(m);
            var span = m!.Span();
            Assert.Equal((6, 11), span);
        }

        #endregion

        #region Match

        [Fact]
        public void Match_AtStart_Matches()
        {
            var m = ReModule.Match("hello", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello", m!.Group());
        }

        [Fact]
        public void Match_NotAtStart_ReturnsNull()
        {
            var m = ReModule.Match("world", "hello world");
            Assert.Null(m);
        }

        #endregion

        #region Fullmatch

        [Fact]
        public void Fullmatch_EntireString_Matches()
        {
            var m = ReModule.Fullmatch(@"\d+", "12345");
            Assert.NotNull(m);
            Assert.Equal("12345", m!.Group());
        }

        [Fact]
        public void Fullmatch_PartialString_ReturnsNull()
        {
            var m = ReModule.Fullmatch(@"\d+", "123abc");
            Assert.Null(m);
        }

        #endregion

        #region Findall

        [Fact]
        public void Findall_MultipleMatches_ReturnsAll()
        {
            var result = ReModule.Findall(@"\d+", "abc 123 def 456");
            Assert.Equal(2, ((ICollection<object?>)result).Count);
            Assert.Equal("123", result[0]);
            Assert.Equal("456", result[1]);
        }

        [Fact]
        public void Findall_NoMatch_ReturnsEmptyList()
        {
            var result = ReModule.Findall(@"\d+", "abcdef");
            Assert.Empty(result);
        }

        [Fact]
        public void Findall_WithSingleGroup_ReturnsGroupValues()
        {
            var result = ReModule.Findall(@"(\w+)@(\w+)", "a@b c@d");
            // Multiple groups → returns list of lists
            Assert.Equal(2, ((ICollection<object?>)result).Count);
        }

        #endregion

        #region Finditer

        [Fact]
        public void Finditer_MultipleMatches_ReturnsMatchObjects()
        {
            var result = ReModule.Finditer(@"\d+", "abc 123 def 456");
            Assert.Equal(2, ((ICollection<ReMatch>)result).Count);
            Assert.Equal("123", result[0].Group());
            Assert.Equal("456", result[1].Group());
        }

        #endregion

        #region Sub

        [Fact]
        public void Sub_ReplaceAll_ReplacesAllOccurrences()
        {
            string result = ReModule.Sub(@"\d+", "NUM", "abc 123 def 456");
            Assert.Equal("abc NUM def NUM", result);
        }

        [Fact]
        public void Sub_WithCount_ReplacesLimited()
        {
            string result = ReModule.Sub(@"\d+", "NUM", "abc 123 def 456", count: 1);
            Assert.Equal("abc NUM def 456", result);
        }

        [Fact]
        public void Sub_BackReference_Works()
        {
            string result = ReModule.Sub(@"(\w+)\s(\w+)", "$2 $1", "hello world");
            Assert.Equal("world hello", result);
        }

        #endregion

        #region Split

        [Fact]
        public void Split_SimplePattern_SplitsString()
        {
            var result = ReModule.Split(@"\s+", "one two  three");
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("one", result[0]);
            Assert.Equal("two", result[1]);
            Assert.Equal("three", result[2]);
        }

        [Fact]
        public void Split_WithMaxsplit_LimitsResults()
        {
            var result = ReModule.Split(@"\s+", "one two three four", maxsplit: 2);
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
            var pattern = ReModule.Compile(@"\d+");
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
            var pattern = ReModule.Compile(@"\d+");
            Assert.Equal(@"\d+", pattern.PatternStr);
        }

        #endregion

        #region Flags

        [Fact]
        public void Search_IgnoreCase_MatchesCaseInsensitive()
        {
            var m = ReModule.Search("hello", "HELLO WORLD", flags: ReModule.IGNORECASE);
            Assert.NotNull(m);
            Assert.Equal("HELLO", m!.Group());
        }

        [Fact]
        public void Search_IgnoreCaseShorthand_Works()
        {
            var m = ReModule.Search("hello", "HELLO WORLD", flags: ReModule.I);
            Assert.NotNull(m);
        }

        [Fact]
        public void Search_Multiline_MatchesAtLineStart()
        {
            var m = ReModule.Search("^world", "hello\nworld", flags: ReModule.MULTILINE);
            Assert.NotNull(m);
            Assert.Equal("world", m!.Group());
        }

        [Fact]
        public void Search_Dotall_DotMatchesNewline()
        {
            var m = ReModule.Fullmatch("a.b", "a\nb", flags: ReModule.DOTALL);
            Assert.NotNull(m);
        }

        [Fact]
        public void Search_CombinedFlags_Work()
        {
            var m = ReModule.Search("^hello", "HELLO\nWORLD", flags: ReModule.IGNORECASE | ReModule.MULTILINE);
            Assert.NotNull(m);
            Assert.Equal("HELLO", m!.Group());
        }

        #endregion

        #region Named Groups

        [Fact]
        public void Search_NamedGroup_PythonSyntax_Works()
        {
            // (?P<name>...) Python syntax → translated to (?<name>...) .NET syntax
            var m = ReModule.Search(@"(?P<first>\w+)\s(?P<last>\w+)", "John Smith");
            Assert.NotNull(m);
            Assert.Equal("John", m!.Group("first"));
            Assert.Equal("Smith", m!.Group("last"));
        }

        [Fact]
        public void Search_NamedGroup_Groupdict_Works()
        {
            var m = ReModule.Search(@"(?P<first>\w+)\s(?P<last>\w+)", "John Smith");
            Assert.NotNull(m);
            var gd = m!.Groupdict();
            Assert.Equal("John", gd["first"]);
            Assert.Equal("Smith", gd["last"]);
        }

        [Fact]
        public void Search_NamedBackref_PythonSyntax_Works()
        {
            // (?P=name) Python syntax → translated to \k<name> .NET syntax
            var m = ReModule.Search(@"(?P<word>\w+)\s(?P=word)", "hello hello");
            Assert.NotNull(m);
            Assert.Equal("hello hello", m!.Group());
        }

        [Fact]
        public void Search_DotNetNamedGroup_AlsoWorks()
        {
            // .NET native syntax should also work
            var m = ReModule.Search(@"(?<first>\w+)\s(?<last>\w+)", "John Smith");
            Assert.NotNull(m);
            Assert.Equal("John", m!.Group("first"));
        }

        #endregion

        #region Groups

        [Fact]
        public void Groups_ReturnsAllSubgroups()
        {
            var m = ReModule.Search(@"(\w+)\s(\w+)", "hello world");
            Assert.NotNull(m);
            var groups = m!.Groups();
            Assert.Equal(2, ((ICollection<string?>)groups).Count);
            Assert.Equal("hello", groups[0]);
            Assert.Equal("world", groups[1]);
        }

        [Fact]
        public void Groups_NonParticipating_ReturnsNull()
        {
            var m = ReModule.Search(@"(\w+)|(\d+)", "hello");
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
            string result = ReModule.Escape("hello.world*");
            // Should be able to use the escaped pattern literally
            var m = ReModule.Search(result, "hello.world*");
            Assert.NotNull(m);
        }

        [Fact]
        public void Escape_PlainText_Unchanged()
        {
            string result = ReModule.Escape("hello");
            Assert.Equal("hello", result);
        }

        #endregion

        #region Match Properties

        [Fact]
        public void Match_String_ReturnsInput()
        {
            var m = ReModule.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal("hello world", m!.String);
        }

        [Fact]
        public void Match_Pattern_ReturnsPattern()
        {
            var m = ReModule.Search("world", "hello world");
            Assert.NotNull(m);
            Assert.Equal("world", m!.Pattern);
        }

        [Fact]
        public void Match_ToString_ReturnsReadableFormat()
        {
            var m = ReModule.Search("world", "hello world");
            Assert.NotNull(m);
            string s = m!.ToString();
            Assert.Contains("span=(6, 11)", s, StringComparison.Ordinal);
            Assert.Contains("match='world'", s, StringComparison.Ordinal);
        }

        #endregion

        #region Pattern Translation (verified via public API)

        [Fact]
        public void PythonNamedGroup_Translated_MatchesCorrectly()
        {
            // (?P<name>...) syntax should work via translation
            var m = ReModule.Search(@"(?P<num>\d+)", "abc 123");
            Assert.NotNull(m);
            Assert.Equal("123", m!.Group("num"));
        }

        [Fact]
        public void PythonNamedBackref_Translated_MatchesCorrectly()
        {
            // (?P=name) syntax should work via translation
            var m = ReModule.Search(@"(?P<w>\w+)\s(?P=w)", "abc abc");
            Assert.NotNull(m);
            Assert.Equal("abc abc", m!.Group());
        }

        [Fact]
        public void NoSpecialSyntax_WorksUnchanged()
        {
            var m = ReModule.Search(@"\d+", "abc 123");
            Assert.NotNull(m);
            Assert.Equal("123", m!.Group());
        }

        #endregion

        #region VERBOSE Flag

        [Fact]
        public void Search_VerboseFlag_IgnoresWhitespaceAndComments()
        {
            // Pattern with comments and whitespace — VERBOSE mode
            string pattern = @"
                \d+   # one or more digits
                \s*   # optional whitespace
                \w+   # one or more word chars
            ";
            var m = ReModule.Search(pattern, "123 hello", flags: ReModule.VERBOSE);
            Assert.NotNull(m);
            Assert.Equal("123 hello", m!.Group());
        }

        [Fact]
        public void Search_VerboseShorthand_Works()
        {
            var m = ReModule.Search(@"\d+  # digits", "abc 42", flags: ReModule.X);
            Assert.NotNull(m);
            Assert.Equal("42", m!.Group());
        }

        #endregion

        #region ASCII/UNICODE Flags (no-op on .NET)

        [Fact]
        public void Search_AsciiFlag_AcceptedWithoutError()
        {
            var m = ReModule.Search(@"\w+", "hello", flags: ReModule.ASCII);
            Assert.NotNull(m);
            Assert.Equal("hello", m!.Group());
        }

        [Fact]
        public void Search_UnicodeFlag_AcceptedWithoutError()
        {
            var m = ReModule.Search(@"\w+", "hello", flags: ReModule.UNICODE);
            Assert.NotNull(m);
            Assert.Equal("hello", m!.Group());
        }

        [Fact]
        public void FlagConstants_HaveCorrectValues()
        {
            Assert.Equal(2, ReModule.IGNORECASE);
            Assert.Equal(2, ReModule.I);
            Assert.Equal(8, ReModule.MULTILINE);
            Assert.Equal(8, ReModule.M);
            Assert.Equal(16, ReModule.DOTALL);
            Assert.Equal(16, ReModule.S);
            Assert.Equal(32, ReModule.UNICODE);
            Assert.Equal(32, ReModule.U);
            Assert.Equal(64, ReModule.VERBOSE);
            Assert.Equal(64, ReModule.X);
            Assert.Equal(256, ReModule.ASCII);
            Assert.Equal(256, ReModule.A);
        }

        #endregion

        #region Subn

        [Fact]
        public void Subn_Basic_ReturnsStringAndCount()
        {
            var (result, count) = ReModule.Subn(@"\d+", "NUM", "abc 123 def 456");
            Assert.Equal("abc NUM def NUM", result);
            Assert.Equal(2, count);
        }

        [Fact]
        public void Subn_WithCount_LimitsReplacements()
        {
            var (result, count) = ReModule.Subn(@"\d+", "NUM", "abc 123 def 456", count: 1);
            Assert.Equal("abc NUM def 456", result);
            Assert.Equal(1, count);
        }

        [Fact]
        public void Subn_NoMatch_ReturnsOriginalAndZero()
        {
            var (result, count) = ReModule.Subn(@"\d+", "NUM", "no digits");
            Assert.Equal("no digits", result);
            Assert.Equal(0, count);
        }

        #endregion

        #region Purge

        [Fact]
        public void Purge_DoesNotThrow()
        {
            // Purge is a no-op on .NET — just verify it doesn't throw
            ReModule.Purge();
        }

        #endregion

        #region Callable Sub

        [Fact]
        public void Sub_Callable_ReplacesWithLambdaResult()
        {
            string result = ReModule.Sub(@"\d+", m => m.Group()!.ToUpperInvariant() + "!", "abc 123 def 456");
            Assert.Equal("abc 123! def 456!", result);
        }

        [Fact]
        public void Sub_Callable_WithCount_LimitsReplacements()
        {
            string result = ReModule.Sub(@"\d+", m => "X", "a1b2c3", count: 2);
            Assert.Equal("aXbXc3", result);
        }

        [Fact]
        public void Sub_Callable_MatchObjectHasCorrectGroup()
        {
            var groups = new System.Collections.Generic.List<string>();
            ReModule.Sub(@"(\w+)", m =>
            {
                groups.Add(m.Group()!);
                return "X";
            }, "hello world");
            Assert.Equal(2, groups.Count);
            Assert.Equal("hello", groups[0]);
            Assert.Equal("world", groups[1]);
        }

        [Fact]
        public void Sub_StringRepl_PythonBackreference_TranslatedCorrectly()
        {
            string result = ReModule.Sub(@"(\w+)", @"\1_suffix", "hello world");
            Assert.Equal("hello_suffix world_suffix", result);
        }

        [Fact]
        public void Sub_StringRepl_NamedBackreference_TranslatedCorrectly()
        {
            string result = ReModule.Sub(@"(?P<w>\w+)", @"\g<w>!", "hello world");
            Assert.Equal("hello! world!", result);
        }

        #endregion

        #region Callable Subn

        [Fact]
        public void Subn_Callable_ReturnsStringAndCount()
        {
            var (result, count) = ReModule.Subn(@"\d+", m => "N", "a1b2c3");
            Assert.Equal("aNbNcN", result);
            Assert.Equal(3, count);
        }

        [Fact]
        public void Subn_Callable_WithCount_LimitsReplacements()
        {
            var (result, count) = ReModule.Subn(@"\d+", m => "N", "a1b2c3", count: 1);
            Assert.Equal("aNb2c3", result);
            Assert.Equal(1, count);
        }

        [Fact]
        public void Subn_StringRepl_PythonBackreference_TranslatedCorrectly()
        {
            var (result, count) = ReModule.Subn(@"(\w+)", @"\1_x", "hello world");
            Assert.Equal("hello_x world_x", result);
            Assert.Equal(2, count);
        }

        #endregion
    }
}
