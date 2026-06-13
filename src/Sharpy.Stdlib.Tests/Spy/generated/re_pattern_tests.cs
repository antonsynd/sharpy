// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using re = global::Sharpy.ReModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Re.RePatternTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Re
    {
        [global::Sharpy.SharpyModule("re.re_pattern_tests")]
        public static partial class RePatternTests
        {
        }
    }

    public static partial class Re
    {
        public partial class RePatternTestsTests
        {
            [Xunit.FactAttribute]
            public void TestPatternMatchAtStartReturnsMatch()
            {
#line (12, 5) - (12, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (13, 5) - (13, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Match("123abc");
#line (14, 5) - (14, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (15, 5) - (15, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("123", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestPatternMatchNotAtStartReturnsNone()
            {
#line (19, 5) - (19, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (20, 5) - (20, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Match("abc123");
#line (21, 5) - (21, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Null(m);
            }

            [Xunit.FactAttribute]
            public void TestPatternMatchWithPosMatchesFromPosition()
            {
#line (25, 5) - (25, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (27, 5) - (27, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Match("abc123", pos: 3);
#line (28, 5) - (28, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (29, 5) - (29, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("123", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestPatternMatchEmptyStringEmptyPatternMatches()
            {
#line (33, 5) - (33, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile(".*");
#line (34, 5) - (34, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Match("");
#line (35, 5) - (35, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (36, 5) - (36, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestPatternFullmatchEntireStringReturnsMatch()
            {
#line (42, 5) - (42, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("[a-z]+");
#line (43, 5) - (43, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Fullmatch("hello");
#line (44, 5) - (44, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (45, 5) - (45, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("hello", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestPatternFullmatchPartialStringReturnsNone()
            {
#line (49, 5) - (49, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("[a-z]+");
#line (50, 5) - (50, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Fullmatch("hello123");
#line (51, 5) - (51, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Null(m);
            }

            [Xunit.FactAttribute]
            public void TestPatternFullmatchWithGroupsCapturesAllGroups()
            {
#line (55, 5) - (55, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(\\d{4})-(\\d{2})-(\\d{2})");
#line (56, 5) - (56, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Fullmatch("2024-01-15");
#line (57, 5) - (57, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (58, 5) - (58, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("2024", m.Group(1));
#line (59, 5) - (59, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("01", m.Group(2));
#line (60, 5) - (60, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("15", m.Group(3));
            }

            [Xunit.FactAttribute]
            public void TestPatternFindallMultipleMatchesReturnsAll()
            {
#line (66, 5) - (66, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (67, 5) - (67, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var result = pattern.Findall("a1b22c333");
#line (68, 5) - (68, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (70, 5) - (70, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("1", global::Sharpy.Builtins.Str(result[0]));
#line (71, 5) - (71, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("22", global::Sharpy.Builtins.Str(result[1]));
#line (72, 5) - (72, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("333", global::Sharpy.Builtins.Str(result[2]));
            }

            [Xunit.FactAttribute]
            public void TestPatternFindallNoMatchesReturnsEmptyList()
            {
#line (76, 5) - (76, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (77, 5) - (77, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var result = pattern.Findall("abcdef");
#line (78, 5) - (78, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestPatternFindallSingleGroupReturnsGroupValues()
            {
#line (82, 5) - (82, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(\\d+)");
#line (83, 5) - (83, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var result = pattern.Findall("a1b2c3");
#line (84, 5) - (84, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (85, 5) - (85, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("1", global::Sharpy.Builtins.Str(result[0]));
#line (86, 5) - (86, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("2", global::Sharpy.Builtins.Str(result[1]));
#line (87, 5) - (87, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("3", global::Sharpy.Builtins.Str(result[2]));
            }

            [Xunit.FactAttribute]
            public void TestPatternFinditerReturnsMatchObjects()
            {
#line (93, 5) - (93, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\w+");
#line (94, 5) - (94, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var matches = pattern.Finditer("hello world");
#line (95, 5) - (95, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(matches));
#line (96, 5) - (96, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("hello", matches[0].Group());
#line (97, 5) - (97, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("world", matches[1].Group());
            }

            [Xunit.FactAttribute]
            public void TestPatternFinditerPositionsAreCorrect()
            {
#line (101, 5) - (101, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (102, 5) - (102, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var matches = pattern.Finditer("abc 123 def 456");
#line (103, 5) - (103, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(matches));
#line (104, 5) - (104, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(4, matches[0].Start());
#line (105, 5) - (105, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(7, matches[0].End());
#line (106, 5) - (106, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(12, matches[1].Start());
#line (107, 5) - (107, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(15, matches[1].End());
            }

            [Xunit.FactAttribute]
            public void TestPatternSubReplacesAll()
            {
#line (113, 5) - (113, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (114, 5) - (114, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string result = pattern.Sub("NUM", "a1b2c3");
#line (115, 5) - (115, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("aNUMbNUMcNUM", result);
            }

            [Xunit.FactAttribute]
            public void TestPatternSubWithCountReplacesLimited()
            {
#line (119, 5) - (119, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (120, 5) - (120, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string result = pattern.Sub("NUM", "a1b2c3", count: 2);
#line (121, 5) - (121, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("aNUMbNUMc3", result);
            }

            [Xunit.FactAttribute]
            public void TestPatternSubCount0ReplacesAll()
            {
#line (125, 5) - (125, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (126, 5) - (126, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string result = pattern.Sub("X", "1a2b3c", count: 0);
#line (127, 5) - (127, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("XaXbXc", result);
            }

            [Xunit.FactAttribute]
            public void TestPatternSplitSplitsOnPattern()
            {
#line (133, 5) - (133, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\s+");
#line (134, 5) - (134, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var result = pattern.Split("one two   three");
#line (135, 5) - (135, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (136, 5) - (136, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("one", result[0]);
#line (137, 5) - (137, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("two", result[1]);
#line (138, 5) - (138, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("three", result[2]);
            }

            [Xunit.FactAttribute]
            public void TestPatternSplitWithMaxsplitLimitsResults()
            {
#line (142, 5) - (142, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile(",");
#line (143, 5) - (143, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var result = pattern.Split("a,b,c,d", maxsplit: 2);
#line (144, 5) - (144, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (145, 5) - (145, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("a", result[0]);
#line (146, 5) - (146, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("b", result[1]);
#line (147, 5) - (147, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("c,d", result[2]);
            }

            [Xunit.FactAttribute]
            public void TestPatternFlagsReflectsCompileFlags()
            {
#line (153, 5) - (153, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+", flags: re.IGNORECASE);
#line (154, 5) - (154, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(re.IGNORECASE, pattern.Flags);
            }

            [Xunit.FactAttribute]
            public void TestPatternFlagsZeroWhenNoFlags()
            {
#line (158, 5) - (158, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (159, 5) - (159, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(0, pattern.Flags);
            }

            [Xunit.FactAttribute]
            public void TestPatternToStringContainsPatternStr()
            {
#line (163, 5) - (163, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (164, 5) - (164, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string s = global::Sharpy.Builtins.Str(pattern);
#line (165, 5) - (165, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Contains("\\d+", s);
            }

            [Xunit.FactAttribute]
            public void TestPatternCompileCombinedFlagsWork()
            {
#line (169, 5) - (169, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("^hello", flags: re.IGNORECASE | re.MULTILINE);
#line (170, 5) - (170, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Search("HELLO\nWORLD");
#line (171, 5) - (171, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (172, 5) - (172, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("HELLO", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestPatternCompileDotallDotMatchesNewline()
            {
#line (176, 5) - (176, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("a.b", flags: re.DOTALL);
#line (177, 5) - (177, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Fullmatch("a\nb");
#line (178, 5) - (178, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
            }

            [Xunit.FactAttribute]
            public void TestMatchGroupOutOfRangeThrowsIndexError()
            {
#line (184, 5) - (184, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Match("(\\d+)", "123");
#line (185, 5) - (185, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (186, 5) - (189, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (187, 9) - (187, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                    m.Group(99);
                }));
            }

            [Xunit.FactAttribute]
            public void TestMatchGroupNegativeIndexThrowsIndexError()
            {
#line (191, 5) - (191, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Match("(\\d+)", "123");
#line (192, 5) - (192, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (193, 5) - (196, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (194, 9) - (194, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                    m.Group(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestMatchGroupZeroIsFullMatch()
            {
#line (198, 5) - (198, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(\\w+)\\s(\\w+)", "hello world");
#line (199, 5) - (199, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (200, 5) - (200, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("hello world", m.Group(0));
            }

            [Xunit.FactAttribute]
            public void TestMatchStartWithGroupReturnsGroupStart()
            {
#line (206, 5) - (206, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(\\w+)\\s(\\w+)", "hello world");
#line (207, 5) - (207, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (208, 5) - (208, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(6, m.Start(2));
            }

            [Xunit.FactAttribute]
            public void TestMatchEndWithGroupReturnsGroupEnd()
            {
#line (212, 5) - (212, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(\\w+)\\s(\\w+)", "hello world");
#line (213, 5) - (213, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (214, 5) - (214, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(11, m.End(2));
            }

            [Xunit.FactAttribute]
            public void TestMatchSpanWithGroupReturnsTuple()
            {
#line (218, 5) - (218, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(\\w+)\\s(\\w+)", "hello world");
#line (219, 5) - (219, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (220, 5) - (220, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal((6, 11), m.Span(2));
            }

            [Xunit.FactAttribute]
            public void TestMatchStartDefaultGroupZeroIsFullMatchStart()
            {
#line (224, 5) - (224, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("world", "hello world");
#line (225, 5) - (225, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (226, 5) - (226, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(6, m.Start(0));
            }

            [Xunit.FactAttribute]
            public void TestPatternNamedGroupMatchReturnsNamedGroup()
            {
#line (232, 5) - (232, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(?P<year>\\d{4})-(?P<month>\\d{2})-(?P<day>\\d{2})");
#line (233, 5) - (233, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Fullmatch("2024-03-15");
#line (234, 5) - (234, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (235, 5) - (235, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("2024", m.Group("year"));
#line (236, 5) - (236, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("03", m.Group("month"));
#line (237, 5) - (237, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("15", m.Group("day"));
            }

            [Xunit.FactAttribute]
            public void TestPatternGroupdictReturnsNamedGroupDict()
            {
#line (241, 5) - (241, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(?P<first>\\w+)\\s(?P<last>\\w+)");
#line (242, 5) - (242, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Search("Jane Doe");
#line (243, 5) - (243, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (244, 5) - (244, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var gd = m.Groupdict();
#line (245, 5) - (245, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("Jane", gd["first"]);
#line (246, 5) - (246, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("Doe", gd["last"]);
            }

            [Xunit.FactAttribute]
            public void TestPatternSearchEmptyStringEmptyPatternMatches()
            {
#line (252, 5) - (252, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("");
#line (253, 5) - (253, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Search("");
#line (254, 5) - (254, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (255, 5) - (255, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestPatternSearchEmptyStringNonEmptyPatternReturnsNone()
            {
#line (259, 5) - (259, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (260, 5) - (260, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Search("");
#line (261, 5) - (261, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Null(m);
            }

            [Xunit.FactAttribute]
            public void TestPatternMatchNoMatchReturnsNullNotException()
            {
#line (265, 5) - (265, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d{10}");
#line (266, 5) - (266, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Match("abc");
#line (267, 5) - (267, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Null(m);
            }

            [Xunit.FactAttribute]
            public void TestMatchReReturnsCompiledPattern()
            {
#line (273, 5) - (273, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (274, 5) - (274, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Search("abc 123");
#line (275, 5) - (275, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (276, 5) - (276, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Same(pattern, m.Re);
            }

            [Xunit.FactAttribute]
            public void TestMatchReFromStaticApiIsNotNone()
            {
#line (280, 5) - (280, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("\\d+", "abc 123");
#line (281, 5) - (281, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (282, 5) - (282, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m.Re);
            }

            [Xunit.FactAttribute]
            public void TestMatchLastindexReturnsLastMatchedGroupIndex()
            {
#line (288, 5) - (288, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(\\w+)\\s(\\w+)", "hello world");
#line (289, 5) - (289, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (290, 5) - (290, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(2, m.Lastindex);
            }

            [Xunit.FactAttribute]
            public void TestMatchLastindexNoGroupsReturnsNone()
            {
#line (294, 5) - (294, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("\\w+", "hello");
#line (295, 5) - (295, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (296, 5) - (296, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Null(m.Lastindex);
            }

            [Xunit.FactAttribute]
            public void TestMatchLastindexFirstAlternativeReturnsCorrectIndex()
            {
#line (301, 5) - (301, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(\\w+)|(\\d+)", "hello");
#line (302, 5) - (302, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (303, 5) - (303, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(1, m.Lastindex);
            }

            [Xunit.FactAttribute]
            public void TestMatchLastgroupNamedGroupReturnsName()
            {
#line (307, 5) - (307, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(?P<first>\\w+)\\s(?P<last>\\w+)");
#line (308, 5) - (308, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Search("hello world");
#line (309, 5) - (309, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (310, 5) - (310, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("last", m.Lastgroup);
            }

            [Xunit.FactAttribute]
            public void TestMatchLastgroupUnnamedGroupReturnsNone()
            {
#line (314, 5) - (314, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(\\w+)\\s(\\w+)");
#line (315, 5) - (315, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Search("hello world");
#line (316, 5) - (316, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (317, 5) - (317, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Null(m.Lastgroup);
            }

            [Xunit.FactAttribute]
            public void TestMatchLastgroupNoGroupsReturnsNone()
            {
#line (321, 5) - (321, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\w+");
#line (322, 5) - (322, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Search("hello");
#line (323, 5) - (323, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (324, 5) - (324, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Null(m.Lastgroup);
            }

            [Xunit.FactAttribute]
            public void TestMatchExpandBackslashDigitExpandsGroup()
            {
#line (330, 5) - (330, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(\\w+)\\s(\\w+)", "hello world");
#line (331, 5) - (331, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (332, 5) - (332, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string result = m.Expand("\\2 \\1");
#line (333, 5) - (333, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("world hello", result);
            }

            [Xunit.FactAttribute]
            public void TestMatchExpandBackslashGExpandsNamedGroup()
            {
#line (337, 5) - (337, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(?P<first>\\w+)\\s(?P<last>\\w+)");
#line (338, 5) - (338, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = pattern.Search("hello world");
#line (339, 5) - (339, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (340, 5) - (340, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string result = m.Expand("\\g<last> \\g<first>");
#line (341, 5) - (341, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("world hello", result);
            }

            [Xunit.FactAttribute]
            public void TestMatchExpandBackslashGNumericReference()
            {
#line (345, 5) - (345, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(\\w+)\\s(\\w+)", "hello world");
#line (346, 5) - (346, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (347, 5) - (347, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string result = m.Expand("\\g<2> \\g<1>");
#line (348, 5) - (348, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("world hello", result);
            }

            [Xunit.FactAttribute]
            public void TestMatchExpandPlainTextPassedThrough()
            {
#line (352, 5) - (352, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("\\w+", "hello");
#line (353, 5) - (353, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (354, 5) - (354, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string result = m.Expand("result: \\0");
#line (355, 5) - (355, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("result: hello", result);
            }

            [Xunit.FactAttribute]
            public void TestMatchGroupByNameNonexistentNameThrowsIndexError()
            {
#line (366, 5) - (366, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(?P<word>\\w+)", "hello");
#line (367, 5) - (367, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (368, 5) - (373, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (369, 9) - (369, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                    m.Group("nonexistent");
                }));
            }

            [Xunit.FactAttribute]
            public void TestPatternPatternStrPropertyReturnsOriginal()
            {
#line (375, 5) - (375, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (376, 5) - (376, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("\\d+", pattern.PatternStr);
            }

            [Xunit.FactAttribute]
            public void TestPatternGroupsReturnsGroupCount()
            {
#line (382, 5) - (382, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(\\w+)\\s(\\w+)");
#line (383, 5) - (383, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(2, pattern.Groups);
            }

            [Xunit.FactAttribute]
            public void TestPatternGroupsNoGroupsReturnsZero()
            {
#line (387, 5) - (387, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\w+");
#line (388, 5) - (388, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(0, pattern.Groups);
            }

            [Xunit.FactAttribute]
            public void TestPatternGroupsNamedGroupsCountedCorrectly()
            {
#line (392, 5) - (392, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(?P<first>\\w+)\\s(?P<last>\\w+)");
#line (393, 5) - (393, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(2, pattern.Groups);
            }

            [Xunit.FactAttribute]
            public void TestPatternGroupindexReturnsNameToNumberMapping()
            {
#line (399, 5) - (399, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(?P<first>\\w+)\\s(?P<last>\\w+)");
#line (400, 5) - (400, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var gi = pattern.Groupindex;
#line (401, 5) - (401, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(1, gi["first"]);
#line (402, 5) - (402, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(2, gi["last"]);
            }

            [Xunit.FactAttribute]
            public void TestPatternGroupindexNoNamedGroupsReturnsEmptyDict()
            {
#line (406, 5) - (406, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(\\w+)\\s(\\w+)");
#line (407, 5) - (407, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var gi = pattern.Groupindex;
#line (408, 5) - (408, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(gi));
            }

            [Xunit.FactAttribute]
            public void TestPatternGroupindexMixedNamedUnnamedOnlyNamed()
            {
#line (412, 5) - (412, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("(?P<name>\\w+)\\s(\\d+)");
#line (413, 5) - (413, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var gi = pattern.Groupindex;
#line (414, 5) - (414, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(gi));
#line (416, 5) - (416, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(2, gi["name"]);
            }

            [Xunit.FactAttribute]
            public void TestPatternSubnReturnsStringAndCount()
            {
#line (422, 5) - (422, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (423, 5) - (423, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                global::System.ValueTuple<string, int> result = pattern.Subn("NUM", "a1b2c3");
#line (424, 5) - (424, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(("aNUMbNUMcNUM", 3), result);
            }

            [Xunit.FactAttribute]
            public void TestPatternSubnWithCountLimitsReplacements()
            {
#line (428, 5) - (428, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (429, 5) - (429, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                global::System.ValueTuple<string, int> result = pattern.Subn("NUM", "a1b2c3", count: 2);
#line (430, 5) - (430, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(("aNUMbNUMc3", 2), result);
            }

            [Xunit.FactAttribute]
            public void TestPatternSubnNoMatchReturnsOriginalAndZero()
            {
#line (434, 5) - (434, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (435, 5) - (435, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                global::System.ValueTuple<string, int> result = pattern.Subn("NUM", "no numbers");
#line (436, 5) - (436, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(("no numbers", 0), result);
            }

            [Xunit.FactAttribute]
            public void TestPatternSubCallableWorks()
            {
#line (442, 5) - (442, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (444, 5) - (444, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string result = pattern.Sub(m => "[" + m.Group() + "]", "a1b2c3");
#line (445, 5) - (445, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("a[1]b[2]c[3]", result);
            }

            [Xunit.FactAttribute]
            public void TestPatternSubCallableWithCountLimitsReplacements()
            {
#line (449, 5) - (449, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (450, 5) - (450, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                string result = pattern.Sub(m => "X", "a1b2c3", count: 1);
#line (451, 5) - (451, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("aXb2c3", result);
            }

            [Xunit.FactAttribute]
            public void TestPatternSubnCallableWorks()
            {
#line (455, 5) - (455, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (456, 5) - (456, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                global::System.ValueTuple<string, int> result = pattern.Subn(m => "X", "a1b2c3");
#line (457, 5) - (457, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(("aXbXcX", 3), result);
            }

            [Xunit.FactAttribute]
            public void TestPatternSubnCallableWithCountLimitsReplacements()
            {
#line (461, 5) - (461, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var pattern = re.Compile("\\d+");
#line (462, 5) - (462, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                global::System.ValueTuple<string, int> result = pattern.Subn(m => "X", "a1b2c3", count: 2);
#line (463, 5) - (463, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal(("aXbXc3", 2), result);
            }

            [Xunit.FactAttribute]
            public void TestInlineFlagCaseInsensitiveWorks()
            {
#line (475, 5) - (475, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(?i)hello", "HELLO world");
#line (476, 5) - (476, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (477, 5) - (477, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("HELLO", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestInlineFlagAsciiStrippedWorks()
            {
#line (482, 5) - (482, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(?a)\\w+", "hello");
#line (483, 5) - (483, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (484, 5) - (484, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("hello", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestInlineFlagUnicodeStrippedWorks()
            {
#line (489, 5) - (489, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(?u)\\w+", "hello");
#line (490, 5) - (490, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (491, 5) - (491, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("hello", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestInlineFlagScopedWithColonWorks()
            {
#line (496, 5) - (496, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(?i:hello) world", "HELLO world");
#line (497, 5) - (497, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (498, 5) - (498, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("HELLO world", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestInlineFlagScopedAsciiStrippedWorks()
            {
#line (503, 5) - (503, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(?a:hello) world", "hello world");
#line (504, 5) - (504, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (505, 5) - (505, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("hello world", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestInlineFlagMixedFlagsStripsOnlyPythonOnly()
            {
#line (510, 5) - (510, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                var m = re.Search("(?ai)hello", "HELLO");
#line (511, 5) - (511, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.NotNull(m);
#line (512, 5) - (512, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_pattern_tests.spy"
                Xunit.Assert.Equal("HELLO", m.Group());
            }
        }
    }
}
