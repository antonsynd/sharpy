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
using static Sharpy.Stdlib.Tests.Spy.Re.ReModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Re
    {
        [global::Sharpy.SharpyModule("re.re_module_tests")]
        public static partial class ReModuleTests
        {
        }
    }

    public static partial class Re
    {
        public partial class ReModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSearchSimplePatternFindsMatch()
            {
#line (9, 5) - (9, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("world", "hello world");
#line (10, 5) - (10, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (11, 5) - (11, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("world", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestSearchNoMatchReturnsNone()
            {
#line (15, 5) - (15, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("xyz", "hello world");
#line (16, 5) - (16, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Null(m);
            }

            [Xunit.FactAttribute]
            public void TestSearchWithGroupsCapturesGroups()
            {
#line (20, 5) - (20, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("(\\w+)\\s(\\w+)", "hello world");
#line (21, 5) - (21, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (22, 5) - (22, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello world", m.Group());
#line (23, 5) - (23, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello", m.Group(1));
#line (24, 5) - (24, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("world", m.Group(2));
            }

            [Xunit.FactAttribute]
            public void TestSearchStartEndMatchPosition()
            {
#line (28, 5) - (28, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("world", "hello world");
#line (29, 5) - (29, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (30, 5) - (30, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(6, m.Start());
#line (31, 5) - (31, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(11, m.End());
            }

            [Xunit.FactAttribute]
            public void TestSearchSpanReturnsTuple()
            {
#line (35, 5) - (35, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("world", "hello world");
#line (36, 5) - (36, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (37, 5) - (37, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                global::System.ValueTuple<int, int> span = m.Span();
#line (38, 5) - (38, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal((6, 11), span);
            }

            [Xunit.FactAttribute]
            public void TestMatchAtStartMatches()
            {
#line (44, 5) - (44, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Match("hello", "hello world");
#line (45, 5) - (45, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (46, 5) - (46, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestMatchNotAtStartReturnsNone()
            {
#line (50, 5) - (50, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Match("world", "hello world");
#line (51, 5) - (51, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Null(m);
            }

            [Xunit.FactAttribute]
            public void TestFullmatchEntireStringMatches()
            {
#line (57, 5) - (57, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Fullmatch("\\d+", "12345");
#line (58, 5) - (58, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (59, 5) - (59, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("12345", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestFullmatchPartialStringReturnsNone()
            {
#line (63, 5) - (63, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Fullmatch("\\d+", "123abc");
#line (64, 5) - (64, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Null(m);
            }

            [Xunit.FactAttribute]
            public void TestFindallMultipleMatchesReturnsAll()
            {
#line (70, 5) - (70, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var result = re.Findall("\\d+", "abc 123 def 456");
#line (71, 5) - (71, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line (73, 5) - (73, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("123", global::Sharpy.Builtins.Str(result[0]));
#line (74, 5) - (74, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("456", global::Sharpy.Builtins.Str(result[1]));
            }

            [Xunit.FactAttribute]
            public void TestFindallNoMatchReturnsEmptyList()
            {
#line (78, 5) - (78, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var result = re.Findall("\\d+", "abcdef");
#line (79, 5) - (79, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestFindallWithSingleGroupReturnsGroupValues()
            {
#line (83, 5) - (83, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var result = re.Findall("(\\w+)@(\\w+)", "a@b c@d");
#line (85, 5) - (85, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestFinditerMultipleMatchesReturnsMatchObjects()
            {
#line (91, 5) - (91, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var result = re.Finditer("\\d+", "abc 123 def 456");
#line (92, 5) - (92, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line (93, 5) - (93, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("123", result[0].Group());
#line (94, 5) - (94, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("456", result[1].Group());
            }

            [Xunit.FactAttribute]
            public void TestSubReplaceAllReplacesAllOccurrences()
            {
#line (100, 5) - (100, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Sub("\\d+", "NUM", "abc 123 def 456");
#line (101, 5) - (101, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("abc NUM def NUM", result);
            }

            [Xunit.FactAttribute]
            public void TestSubWithCountReplacesLimited()
            {
#line (105, 5) - (105, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Sub("\\d+", "NUM", "abc 123 def 456", count: 1);
#line (106, 5) - (106, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("abc NUM def 456", result);
            }

            [Xunit.FactAttribute]
            public void TestSubBackReferenceWorks()
            {
#line (110, 5) - (110, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Sub("(\\w+)\\s(\\w+)", "$2 $1", "hello world");
#line (111, 5) - (111, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("world hello", result);
            }

            [Xunit.FactAttribute]
            public void TestSplitSimplePatternSplitsString()
            {
#line (117, 5) - (117, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var result = re.Split("\\s+", "one two  three");
#line (118, 5) - (118, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (119, 5) - (119, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("one", result[0]);
#line (120, 5) - (120, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("two", result[1]);
#line (121, 5) - (121, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("three", result[2]);
            }

            [Xunit.FactAttribute]
            public void TestSplitWithMaxsplitLimitsResults()
            {
#line (125, 5) - (125, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var result = re.Split("\\s+", "one two three four", maxsplit: 2);
#line (126, 5) - (126, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (127, 5) - (127, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("one", result[0]);
#line (128, 5) - (128, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("two", result[1]);
#line (129, 5) - (129, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("three four", result[2]);
            }

            [Xunit.FactAttribute]
            public void TestCompileReturnPatternCanBeReused()
            {
#line (135, 5) - (135, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var pattern = re.Compile("\\d+");
#line (136, 5) - (136, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m1 = pattern.Search("abc 123");
#line (137, 5) - (137, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m2 = pattern.Search("def 456");
#line (138, 5) - (138, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m1);
#line (139, 5) - (139, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m2);
#line (140, 5) - (140, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("123", m1.Group());
#line (141, 5) - (141, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("456", m2.Group());
            }

            [Xunit.FactAttribute]
            public void TestCompilePatternStrReturnsOriginal()
            {
#line (145, 5) - (145, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var pattern = re.Compile("\\d+");
#line (146, 5) - (146, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("\\d+", pattern.PatternStr);
            }

            [Xunit.FactAttribute]
            public void TestSearchIgnoreCaseMatchesCaseInsensitive()
            {
#line (152, 5) - (152, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("hello", "HELLO WORLD", flags: re.IGNORECASE);
#line (153, 5) - (153, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (154, 5) - (154, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("HELLO", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestSearchIgnoreCaseShorthandWorks()
            {
#line (158, 5) - (158, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("hello", "HELLO WORLD", flags: re.I);
#line (159, 5) - (159, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
            }

            [Xunit.FactAttribute]
            public void TestSearchMultilineMatchesAtLineStart()
            {
#line (163, 5) - (163, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("^world", "hello\nworld", flags: re.MULTILINE);
#line (164, 5) - (164, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (165, 5) - (165, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("world", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestSearchDotallDotMatchesNewline()
            {
#line (169, 5) - (169, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Fullmatch("a.b", "a\nb", flags: re.DOTALL);
#line (170, 5) - (170, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
            }

            [Xunit.FactAttribute]
            public void TestSearchCombinedFlagsWork()
            {
#line (174, 5) - (174, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("^hello", "HELLO\nWORLD", flags: re.IGNORECASE | re.MULTILINE);
#line (175, 5) - (175, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (176, 5) - (176, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("HELLO", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestSearchNamedGroupPythonSyntaxWorks()
            {
#line (183, 5) - (183, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("(?P<first>\\w+)\\s(?P<last>\\w+)", "John Smith");
#line (184, 5) - (184, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (185, 5) - (185, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("John", m.Group("first"));
#line (186, 5) - (186, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("Smith", m.Group("last"));
            }

            [Xunit.FactAttribute]
            public void TestSearchNamedGroupGroupdictWorks()
            {
#line (190, 5) - (190, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("(?P<first>\\w+)\\s(?P<last>\\w+)", "John Smith");
#line (191, 5) - (191, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (192, 5) - (192, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var gd = m.Groupdict();
#line (193, 5) - (193, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("John", gd["first"]);
#line (194, 5) - (194, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("Smith", gd["last"]);
            }

            [Xunit.FactAttribute]
            public void TestSearchNamedBackrefPythonSyntaxWorks()
            {
#line (199, 5) - (199, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("(?P<word>\\w+)\\s(?P=word)", "hello hello");
#line (200, 5) - (200, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (201, 5) - (201, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello hello", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestSearchDotnetNamedGroupAlsoWorks()
            {
#line (206, 5) - (206, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("(?<first>\\w+)\\s(?<last>\\w+)", "John Smith");
#line (207, 5) - (207, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (208, 5) - (208, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("John", m.Group("first"));
            }

            [Xunit.FactAttribute]
            public void TestGroupsReturnsAllSubgroups()
            {
#line (214, 5) - (214, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("(\\w+)\\s(\\w+)", "hello world");
#line (215, 5) - (215, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (216, 5) - (216, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var groups = m.Groups();
#line (217, 5) - (217, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(groups));
#line (218, 5) - (218, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello", groups[0]);
#line (219, 5) - (219, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("world", groups[1]);
            }

            [Xunit.FactAttribute]
            public void TestGroupsNonParticipatingReturnsNone()
            {
#line (223, 5) - (223, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("(\\w+)|(\\d+)", "hello");
#line (224, 5) - (224, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (225, 5) - (225, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var groups = m.Groups();
#line (226, 5) - (226, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello", groups[0]);
#line (227, 5) - (227, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Null(groups[1]);
            }

            [Xunit.FactAttribute]
            public void TestEscapeSpecialCharsAreEscaped()
            {
#line (233, 5) - (233, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Escape("hello.world*");
#line (235, 5) - (235, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search(result, "hello.world*");
#line (236, 5) - (236, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
            }

            [Xunit.FactAttribute]
            public void TestEscapePlainTextUnchanged()
            {
#line (240, 5) - (240, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Escape("hello");
#line (241, 5) - (241, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello", result);
            }

            [Xunit.FactAttribute]
            public void TestMatchStringReturnsInput()
            {
#line (247, 5) - (247, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("world", "hello world");
#line (248, 5) - (248, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (249, 5) - (249, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello world", m.String);
            }

            [Xunit.FactAttribute]
            public void TestMatchPatternReturnsPattern()
            {
#line (253, 5) - (253, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("world", "hello world");
#line (254, 5) - (254, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (255, 5) - (255, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("world", m.Pattern);
            }

            [Xunit.FactAttribute]
            public void TestMatchToStringReturnsReadableFormat()
            {
#line (259, 5) - (259, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("world", "hello world");
#line (260, 5) - (260, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (261, 5) - (261, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string s = global::Sharpy.Builtins.Str(m);
#line (262, 5) - (262, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Contains("span=(6, 11)", s);
#line (263, 5) - (263, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Contains("match='world'", s);
            }

            [Xunit.FactAttribute]
            public void TestPythonNamedGroupTranslatedMatchesCorrectly()
            {
#line (270, 5) - (270, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("(?P<num>\\d+)", "abc 123");
#line (271, 5) - (271, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (272, 5) - (272, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("123", m.Group("num"));
            }

            [Xunit.FactAttribute]
            public void TestPythonNamedBackrefTranslatedMatchesCorrectly()
            {
#line (277, 5) - (277, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("(?P<w>\\w+)\\s(?P=w)", "abc abc");
#line (278, 5) - (278, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (279, 5) - (279, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("abc abc", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestNoSpecialSyntaxWorksUnchanged()
            {
#line (283, 5) - (283, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("\\d+", "abc 123");
#line (284, 5) - (284, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (285, 5) - (285, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("123", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestSearchVerboseFlagIgnoresWhitespaceAndComments()
            {
#line (292, 5) - (292, 115) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string pattern = "\\d+   # one or more digits\n\\s*   # optional whitespace\n\\w+   # one or more word chars\n";
#line (293, 5) - (293, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search(pattern, "123 hello", flags: re.VERBOSE);
#line (294, 5) - (294, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (295, 5) - (295, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("123 hello", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestSearchVerboseShorthandWorks()
            {
#line (299, 5) - (299, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("\\d+  # digits", "abc 42", flags: re.X);
#line (300, 5) - (300, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (301, 5) - (301, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("42", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestSearchAsciiFlagAcceptedWithoutError()
            {
#line (307, 5) - (307, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("\\w+", "hello", flags: re.ASCII);
#line (308, 5) - (308, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (309, 5) - (309, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestSearchUnicodeFlagAcceptedWithoutError()
            {
#line (313, 5) - (313, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                var m = re.Search("\\w+", "hello", flags: re.UNICODE);
#line (314, 5) - (314, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.NotNull(m);
#line (315, 5) - (315, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello", m.Group());
            }

            [Xunit.FactAttribute]
            public void TestFlagConstantsHaveCorrectValues()
            {
#line (319, 5) - (319, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(2, re.IGNORECASE);
#line (320, 5) - (320, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(2, re.I);
#line (321, 5) - (321, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(8, re.MULTILINE);
#line (322, 5) - (322, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(8, re.M);
#line (323, 5) - (323, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(16, re.DOTALL);
#line (324, 5) - (324, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(16, re.S);
#line (325, 5) - (325, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(32, re.UNICODE);
#line (326, 5) - (326, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(32, re.U);
#line (327, 5) - (327, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(64, re.VERBOSE);
#line (328, 5) - (328, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(64, re.X);
#line (329, 5) - (329, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(256, re.ASCII);
#line (330, 5) - (330, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(256, re.A);
            }

            [Xunit.FactAttribute]
            public void TestSubnBasicReturnsStringAndCount()
            {
#line (336, 5) - (336, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                global::System.ValueTuple<string, int> result = re.Subn("\\d+", "NUM", "abc 123 def 456");
#line (337, 5) - (337, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(("abc NUM def NUM", 2), result);
            }

            [Xunit.FactAttribute]
            public void TestSubnWithCountLimitsReplacements()
            {
#line (341, 5) - (341, 82) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                global::System.ValueTuple<string, int> result = re.Subn("\\d+", "NUM", "abc 123 def 456", count: 1);
#line (342, 5) - (342, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(("abc NUM def 456", 1), result);
            }

            [Xunit.FactAttribute]
            public void TestSubnNoMatchReturnsOriginalAndZero()
            {
#line (346, 5) - (346, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                global::System.ValueTuple<string, int> result = re.Subn("\\d+", "NUM", "no digits");
#line (347, 5) - (347, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(("no digits", 0), result);
            }

            [Xunit.FactAttribute]
            public void TestPurgeDoesNotThrow()
            {
#line (354, 5) - (354, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                re.Purge();
            }

            [Xunit.FactAttribute]
            public void TestSubCallableReplacesWithLambdaResult()
            {
#line (362, 5) - (362, 88) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Sub("\\d+", m => m.Group().Upper() + "!", "abc 123 def 456");
#line (363, 5) - (363, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("abc 123! def 456!", result);
            }

            [Xunit.FactAttribute]
            public void TestSubCallableWithCountLimitsReplacements()
            {
#line (367, 5) - (367, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Sub("\\d+", m => "X", "a1b2c3", count: 2);
#line (368, 5) - (368, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("aXbXc3", result);
            }

            [Xunit.FactAttribute]
            public void TestSubCallableMatchObjectHasCorrectGroup()
            {
#line (374, 5) - (374, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Sub("(\\w+)", m => m.Group() + "|", "hello world");
#line (375, 5) - (375, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello| world|", result);
            }

            [Xunit.FactAttribute]
            public void TestSubStringReplPythonBackreferenceTranslatedCorrectly()
            {
#line (379, 5) - (379, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Sub("(\\w+)", "\\1_suffix", "hello world");
#line (380, 5) - (380, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello_suffix world_suffix", result);
            }

            [Xunit.FactAttribute]
            public void TestSubStringReplNamedBackreferenceTranslatedCorrectly()
            {
#line (384, 5) - (384, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                string result = re.Sub("(?P<w>\\w+)", "\\g<w>!", "hello world");
#line (385, 5) - (385, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal("hello! world!", result);
            }

            [Xunit.FactAttribute]
            public void TestSubnCallableReturnsStringAndCount()
            {
#line (391, 5) - (391, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                global::System.ValueTuple<string, int> result = re.Subn("\\d+", m => "N", "a1b2c3");
#line (392, 5) - (392, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(("aNbNcN", 3), result);
            }

            [Xunit.FactAttribute]
            public void TestSubnCallableWithCountLimitsReplacements()
            {
#line (396, 5) - (396, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                global::System.ValueTuple<string, int> result = re.Subn("\\d+", m => "N", "a1b2c3", count: 1);
#line (397, 5) - (397, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(("aNb2c3", 1), result);
            }

            [Xunit.FactAttribute]
            public void TestSubnStringReplPythonBackreferenceTranslatedCorrectly()
            {
#line (401, 5) - (401, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                global::System.ValueTuple<string, int> result = re.Subn("(\\w+)", "\\1_x", "hello world");
#line (402, 5) - (402, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/re/re_module_tests.spy"
                Xunit.Assert.Equal(("hello_x world_x", 2), result);
            }
        }
    }
}
