// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using re = global::Sharpy.ReModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Re.ReOperationTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Re
    {
        [global::Sharpy.SharpyModule("re.re_operation_tests")]
        public static partial class ReOperationTests
        {
        }
    }

    public static partial class Re
    {
        public partial class ReOperationTestsTests
        {
            [Xunit.FactAttribute]
            public void TestFindallNoGroupsReturnsFullMatches()
            {
#line (10, 5) - (10, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Findall("\\d+", "a1b2c3");
#line (11, 5) - (11, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (13, 5) - (13, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("1", global::Sharpy.Builtins.Str(result[0]));
#line (14, 5) - (14, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("2", global::Sharpy.Builtins.Str(result[1]));
#line (15, 5) - (15, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("3", global::Sharpy.Builtins.Str(result[2]));
            }

            [Xunit.FactAttribute]
            public void TestFindallSingleGroupReturnsGroupValues()
            {
#line (20, 5) - (20, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Findall("(\\d+)", "a1b2c3");
#line (21, 5) - (21, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (22, 5) - (22, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("1", global::Sharpy.Builtins.Str(result[0]));
#line (23, 5) - (23, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("2", global::Sharpy.Builtins.Str(result[1]));
#line (24, 5) - (24, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("3", global::Sharpy.Builtins.Str(result[2]));
            }

            [Xunit.FactAttribute]
            public void TestFindallNoMatchReturnsEmptyList()
            {
#line (28, 5) - (28, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Findall("\\d+", "no digits here");
#line (29, 5) - (29, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestFindallWithFlagsIgnoreCase()
            {
#line (33, 5) - (33, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Findall("[a-z]+", "Hello World", flags: re.IGNORECASE);
#line (34, 5) - (34, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line (35, 5) - (35, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("Hello", global::Sharpy.Builtins.Str(result[0]));
#line (36, 5) - (36, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("World", global::Sharpy.Builtins.Str(result[1]));
            }

            [Xunit.FactAttribute]
            public void TestFinditerReturnsMatchObjectsWithCorrectGroups()
            {
#line (42, 5) - (42, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var matches = re.Finditer("(\\w+)=(\\d+)", "x=1 y=42 z=100");
#line (43, 5) - (43, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(matches));
#line (44, 5) - (44, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("x", matches[0].Group(1));
#line (45, 5) - (45, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("1", matches[0].Group(2));
#line (46, 5) - (46, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("y", matches[1].Group(1));
#line (47, 5) - (47, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("42", matches[1].Group(2));
            }

            [Xunit.FactAttribute]
            public void TestFinditerNoMatchReturnsEmptyList()
            {
#line (51, 5) - (51, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var matches = re.Finditer("\\d+", "no numbers");
#line (52, 5) - (52, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(matches));
            }

            [Xunit.FactAttribute]
            public void TestFinditerSpansAreCorrect()
            {
#line (56, 5) - (56, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var matches = re.Finditer("\\d+", "ab12cd34");
#line (57, 5) - (57, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(matches));
#line (58, 5) - (58, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal((2, 4), matches[0].Span());
#line (59, 5) - (59, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal((6, 8), matches[1].Span());
            }

            [Xunit.FactAttribute]
            public void TestSplitCommaSeparatedSplitsAll()
            {
#line (65, 5) - (65, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Split(",", "a,b,c");
#line (66, 5) - (66, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (67, 5) - (67, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("a", result[0]);
#line (68, 5) - (68, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("b", result[1]);
#line (69, 5) - (69, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("c", result[2]);
            }

            [Xunit.FactAttribute]
            public void TestSplitNoMatchReturnsSingleElementList()
            {
#line (73, 5) - (73, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Split("\\d+", "no numbers");
#line (74, 5) - (74, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (75, 5) - (75, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("no numbers", result[0]);
            }

            [Xunit.FactAttribute]
            public void TestSplitWithMaxsplit1SplitsOnce()
            {
#line (79, 5) - (79, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Split(",", "a,b,c", maxsplit: 1);
#line (80, 5) - (80, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line (81, 5) - (81, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("a", result[0]);
#line (82, 5) - (82, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("b,c", result[1]);
            }

            [Xunit.FactAttribute]
            public void TestSplitWithCapturingGroupIncludesDelimiters()
            {
#line (87, 5) - (87, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Split("(\\W+)", "Words, words");
#line (88, 5) - (88, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (89, 5) - (89, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("Words", result[0]);
#line (90, 5) - (90, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(", ", result[1]);
#line (91, 5) - (91, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("words", result[2]);
            }

            [Xunit.FactAttribute]
            public void TestSplitPatternAtStartLeadsWithEmptyString()
            {
#line (95, 5) - (95, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Split(",", ",a,b");
#line (96, 5) - (96, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (97, 5) - (97, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("", result[0]);
#line (98, 5) - (98, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("a", result[1]);
#line (99, 5) - (99, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("b", result[2]);
            }

            [Xunit.FactAttribute]
            public void TestSplitPatternAtEndTrailsWithEmptyString()
            {
#line (103, 5) - (103, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var result = re.Split(",", "a,b,");
#line (104, 5) - (104, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (105, 5) - (105, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("a", result[0]);
#line (106, 5) - (106, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("b", result[1]);
#line (107, 5) - (107, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("", result[2]);
            }

            [Xunit.FactAttribute]
            public void TestSubReplacesAllOccurrences()
            {
#line (113, 5) - (113, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Sub("\\d+", "NUM", "a1b2c3");
#line (114, 5) - (114, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("aNUMbNUMcNUM", result);
            }

            [Xunit.FactAttribute]
            public void TestSubCount1ReplacesFirstOnly()
            {
#line (118, 5) - (118, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Sub("\\d+", "NUM", "a1b2c3", count: 1);
#line (119, 5) - (119, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("aNUMb2c3", result);
            }

            [Xunit.FactAttribute]
            public void TestSubNoMatchReturnsOriginalString()
            {
#line (123, 5) - (123, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Sub("\\d+", "NUM", "no numbers");
#line (124, 5) - (124, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("no numbers", result);
            }

            [Xunit.FactAttribute]
            public void TestSubWithBackreferenceSwapsGroups()
            {
#line (128, 5) - (128, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Sub("(\\w+)\\s(\\w+)", "$2 $1", "hello world");
#line (129, 5) - (129, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("world hello", result);
            }

            [Xunit.FactAttribute]
            public void TestSubWithFlagsCaseInsensitive()
            {
#line (133, 5) - (133, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Sub("[a-z]+", "X", "Hello World", flags: re.IGNORECASE);
#line (134, 5) - (134, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("X X", result);
            }

            [Xunit.FactAttribute]
            public void TestSubEmptyStringReturnsEmptyString()
            {
#line (138, 5) - (138, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Sub("\\d+", "X", "");
#line (139, 5) - (139, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("", result);
            }

            [Xunit.FactAttribute]
            public void TestEscapeEmptyStringReturnsEmptyString()
            {
#line (145, 5) - (145, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Escape("");
#line (146, 5) - (146, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("", result);
            }

            [Xunit.FactAttribute]
            public void TestEscapePlainAlphanumericUnchanged()
            {
#line (150, 5) - (150, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Escape("hello123");
#line (151, 5) - (151, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal("hello123", result);
            }

            [Xunit.FactAttribute]
            public void TestEscapeDotIsEscaped()
            {
#line (155, 5) - (155, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Escape(".");
#line (157, 5) - (157, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var m = re.Search(result, ".");
#line (158, 5) - (158, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.NotNull(m);
#line (160, 5) - (160, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var noMatch = re.Compile(result).Match("a");
#line (161, 5) - (161, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Null(noMatch);
            }

            [Xunit.FactAttribute]
            public void TestEscapeSpecialCharsCanBeUsedAsLiteralPattern()
            {
#line (166, 5) - (166, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string literal = "a.b*c+d?";
#line (167, 5) - (167, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string escaped = re.Escape(literal);
#line (168, 5) - (168, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var m = re.Search(escaped, literal);
#line (169, 5) - (169, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.NotNull(m);
#line (170, 5) - (170, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.Equal(literal, m.Group());
            }

            [Xunit.FactAttribute]
            public void TestEscapeCaretIsEscaped()
            {
#line (174, 5) - (174, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                string result = re.Escape("^");
#line (176, 5) - (176, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                var m = re.Search(result, "a^b");
#line (177, 5) - (177, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_operation_tests.spy"
                Xunit.Assert.NotNull(m);
            }
        }
    }
}
