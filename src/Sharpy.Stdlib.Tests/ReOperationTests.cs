using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    /// <summary>
    /// Tests for Re module operations: Findall, Finditer, Split, Sub, Escape.
    /// These complement ReModuleTests.cs with edge cases and additional scenarios.
    /// </summary>
    public class ReOperationTests
    {
        #region Findall Edge Cases

        [Fact]
        public void Findall_NoGroups_ReturnsFullMatches()
        {
            var result = ReModule.Findall(@"\d+", "a1b2c3");
            Assert.Equal(3, ((ICollection<object?>)result).Count);
            Assert.Equal("1", result[0]);
            Assert.Equal("2", result[1]);
            Assert.Equal("3", result[2]);
        }

        [Fact]
        public void Findall_SingleGroup_ReturnsGroupValues()
        {
            // Python: re.findall(r'(\d+)', 'a1b2c3') == ['1', '2', '3']
            var result = ReModule.Findall(@"(\d+)", "a1b2c3");
            Assert.Equal(3, ((ICollection<object?>)result).Count);
            Assert.Equal("1", result[0]);
            Assert.Equal("2", result[1]);
            Assert.Equal("3", result[2]);
        }

        [Fact]
        public void Findall_NoMatch_ReturnsEmptyList()
        {
            var result = ReModule.Findall(@"\d+", "no digits here");
            Assert.Empty(result);
        }

        [Fact]
        public void Findall_WithFlags_IgnoreCase()
        {
            var result = ReModule.Findall("[a-z]+", "Hello World", flags: ReModule.IGNORECASE);
            Assert.Equal(2, ((ICollection<object?>)result).Count);
            Assert.Equal("Hello", result[0]);
            Assert.Equal("World", result[1]);
        }

        #endregion

        #region Finditer Edge Cases

        [Fact]
        public void Finditer_ReturnsMatchObjectsWithCorrectGroups()
        {
            var matches = ReModule.Finditer(@"(\w+)=(\d+)", "x=1 y=42 z=100");
            Assert.Equal(3, ((ICollection<ReMatch>)matches).Count);
            Assert.Equal("x", matches[0].Group(1));
            Assert.Equal("1", matches[0].Group(2));
            Assert.Equal("y", matches[1].Group(1));
            Assert.Equal("42", matches[1].Group(2));
        }

        [Fact]
        public void Finditer_NoMatch_ReturnsEmptyList()
        {
            var matches = ReModule.Finditer(@"\d+", "no numbers");
            Assert.Empty(matches);
        }

        [Fact]
        public void Finditer_SpansAreCorrect()
        {
            var matches = ReModule.Finditer(@"\d+", "ab12cd34");
            Assert.Equal(2, ((ICollection<ReMatch>)matches).Count);
            Assert.Equal((2, 4), matches[0].Span());
            Assert.Equal((6, 8), matches[1].Span());
        }

        #endregion

        #region Split Edge Cases

        [Fact]
        public void Split_CommaSeparated_SplitsAll()
        {
            var result = ReModule.Split(",", "a,b,c");
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("b", result[1]);
            Assert.Equal("c", result[2]);
        }

        [Fact]
        public void Split_NoMatch_ReturnsSingleElementList()
        {
            var result = ReModule.Split(@"\d+", "no numbers");
            Assert.Single((ICollection<string>)result);
            Assert.Equal("no numbers", result[0]);
        }

        [Fact]
        public void Split_WithMaxsplit1_SplitsOnce()
        {
            var result = ReModule.Split(",", "a,b,c", maxsplit: 1);
            Assert.Equal(2, ((ICollection<string>)result).Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("b,c", result[1]);
        }

        [Fact]
        public void Split_WithCapturingGroup_IncludesDelimiters()
        {
            // Python: re.split(r'(\W+)', 'Words, words') == ['Words', ', ', 'words']
            var result = ReModule.Split(@"(\W+)", "Words, words");
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("Words", result[0]);
            Assert.Equal(", ", result[1]);
            Assert.Equal("words", result[2]);
        }

        [Fact]
        public void Split_PatternAtStart_LeadsWithEmptyString()
        {
            var result = ReModule.Split(",", ",a,b");
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("", result[0]);
            Assert.Equal("a", result[1]);
            Assert.Equal("b", result[2]);
        }

        [Fact]
        public void Split_PatternAtEnd_TrailsWithEmptyString()
        {
            var result = ReModule.Split(",", "a,b,");
            Assert.Equal(3, ((ICollection<string>)result).Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("b", result[1]);
            Assert.Equal("", result[2]);
        }

        #endregion

        #region Sub Edge Cases

        [Fact]
        public void Sub_ReplacesAllOccurrences()
        {
            string result = ReModule.Sub(@"\d+", "NUM", "a1b2c3");
            Assert.Equal("aNUMbNUMcNUM", result);
        }

        [Fact]
        public void Sub_Count1_ReplacesFirstOnly()
        {
            string result = ReModule.Sub(@"\d+", "NUM", "a1b2c3", count: 1);
            Assert.Equal("aNUMb2c3", result);
        }

        [Fact]
        public void Sub_NoMatch_ReturnsOriginalString()
        {
            string result = ReModule.Sub(@"\d+", "NUM", "no numbers");
            Assert.Equal("no numbers", result);
        }

        [Fact]
        public void Sub_WithBackreference_SwapsGroups()
        {
            string result = ReModule.Sub(@"(\w+)\s(\w+)", "$2 $1", "hello world");
            Assert.Equal("world hello", result);
        }

        [Fact]
        public void Sub_WithFlags_CaseInsensitive()
        {
            string result = ReModule.Sub("[a-z]+", "X", "Hello World", flags: ReModule.IGNORECASE);
            Assert.Equal("X X", result);
        }

        [Fact]
        public void Sub_EmptyString_ReturnsEmptyString()
        {
            string result = ReModule.Sub(@"\d+", "X", "");
            Assert.Equal("", result);
        }

        #endregion

        #region Escape Edge Cases

        [Fact]
        public void Escape_EmptyString_ReturnsEmptyString()
        {
            string result = ReModule.Escape("");
            Assert.Equal("", result);
        }

        [Fact]
        public void Escape_PlainAlphanumeric_Unchanged()
        {
            string result = ReModule.Escape("hello123");
            Assert.Equal("hello123", result);
        }

        [Fact]
        public void Escape_Dot_IsEscaped()
        {
            string result = ReModule.Escape(".");
            // Escaped dot can be used as literal pattern
            var m = ReModule.Search(result, ".");
            Assert.NotNull(m);
            // It should NOT match a non-dot
            var noMatch = ReModule.Match(result, "a");
            Assert.Null(noMatch);
        }

        [Fact]
        public void Escape_SpecialChars_CanBeUsedAsLiteralPattern()
        {
            // Escaped pattern should match the literal string
            string literal = "a.b*c+d?";
            string escaped = ReModule.Escape(literal);
            var m = ReModule.Search(escaped, literal);
            Assert.NotNull(m);
            Assert.Equal(literal, m!.Group());
        }

        [Fact]
        public void Escape_Caret_IsEscaped()
        {
            string result = ReModule.Escape("^");
            // The escaped string should match literal "^"
            var m = ReModule.Search(result, "a^b");
            Assert.NotNull(m);
        }

        #endregion
    }
}
