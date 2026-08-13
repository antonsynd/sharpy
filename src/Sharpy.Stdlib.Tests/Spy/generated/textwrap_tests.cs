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
using textwrap = global::Sharpy.Textwrap;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Textwrap.TextwrapTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Textwrap
    {
        [global::Sharpy.SharpyModule("textwrap.textwrap_tests")]
        public static partial class TextwrapTests
        {
        }
    }

    public static partial class Textwrap
    {
        public partial class TextwrapTestsTests
        {
            [Xunit.FactAttribute]
            public void TestWrapBasicWordWrap()
            {
#line (9, 5) - (9, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Sharpy.List<string> result = textwrap.Wrap("Hello World", 5);
#line (10, 5) - (10, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line (11, 5) - (11, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("Hello", result.GetItemUnchecked(0));
#line (12, 5) - (12, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("World", result.GetItemUnchecked(1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWrapEmptyStringReturnsEmptyList()
            {
#line (16, 5) - (16, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Sharpy.List<string> result = textwrap.Wrap("", 5);
#line (17, 5) - (17, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWrapWhitespaceOnlyReturnsEmptyList()
            {
#line (21, 5) - (21, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Sharpy.List<string> result = textwrap.Wrap("   ", 5);
#line (22, 5) - (22, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWrapLongWordBreaksWord()
            {
#line (26, 5) - (26, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Sharpy.List<string> result = textwrap.Wrap("abcdefgh", 5);
#line (27, 5) - (27, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line (28, 5) - (28, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("abcde", result.GetItemUnchecked(0));
#line (29, 5) - (29, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("fgh", result.GetItemUnchecked(1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWrapVeryLongWordBreaksIntoMultipleChunks()
            {
#line (33, 5) - (33, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Sharpy.List<string> result = textwrap.Wrap("abcdefghijklmnop", 5);
#line (34, 5) - (34, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(result));
#line (35, 5) - (35, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("abcde", result.GetItemUnchecked(0));
#line (36, 5) - (36, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("fghij", result.GetItemUnchecked(1));
#line (37, 5) - (37, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("klmno", result.GetItemUnchecked(2));
#line (38, 5) - (38, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("p", result.GetItemUnchecked(3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWrapDefaultWidth70()
            {
#line (42, 5) - (42, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string text = global::Sharpy.StringHelpers.Repeat("a", 100);
#line (43, 5) - (43, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Sharpy.List<string> result = textwrap.Wrap(text);
#line (44, 5) - (44, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line (45, 5) - (45, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(70, result.GetItemUnchecked(0).Length);
#line (46, 5) - (46, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(30, result.GetItemUnchecked(1).Length);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWrapCollapsesWhitespace()
            {
#line (50, 5) - (50, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Sharpy.List<string> result = textwrap.Wrap("hello   world", 70);
#line (51, 5) - (51, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (52, 5) - (52, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("hello world", result.GetItemUnchecked(0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWrapCollapsesNewlines()
            {
#line (56, 5) - (56, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Sharpy.List<string> result = textwrap.Wrap("hello\nworld", 70);
#line (57, 5) - (57, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (58, 5) - (58, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("hello world", result.GetItemUnchecked(0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFillJoinsWithNewlines()
            {
#line (64, 5) - (64, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Fill("Hello World", 5);
#line (65, 5) - (65, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("Hello\nWorld", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFillEmptyStringReturnsEmpty()
            {
#line (69, 5) - (69, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Fill("", 5);
#line (70, 5) - (70, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDedentRemovesCommonIndentation()
            {
#line (76, 5) - (76, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Dedent("  hello\n  world");
#line (77, 5) - (77, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("hello\nworld", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDedentPartialCommonIndentation()
            {
#line (81, 5) - (81, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Dedent("  hello\n    world");
#line (82, 5) - (82, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("hello\n  world", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDedentEmptyLinesIgnored()
            {
#line (86, 5) - (86, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Dedent("  hello\n\n  world");
#line (87, 5) - (87, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("hello\n\nworld", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDedentNoCommonIndentation()
            {
#line (91, 5) - (91, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Dedent("hello\nworld");
#line (92, 5) - (92, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("hello\nworld", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDedentTabIndentation()
            {
#line (96, 5) - (96, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Dedent("\thello\n\tworld");
#line (97, 5) - (97, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("hello\nworld", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDedentEmptyString()
            {
#line (101, 5) - (101, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Dedent("");
#line (102, 5) - (102, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndentAddsPrefix()
            {
#line (108, 5) - (108, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Indent("hello\nworld", "  ");
#line (109, 5) - (109, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("  hello\n  world", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndentSkipsEmptyLines()
            {
#line (113, 5) - (113, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Indent("hello\n\nworld", "> ");
#line (114, 5) - (114, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("> hello\n\n> world", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndentSkipsWhitespaceOnlyLines()
            {
#line (118, 5) - (118, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Indent("hello\n  \nworld", "> ");
#line (119, 5) - (119, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("> hello\n  \n> world", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndentPreservesTrailingNewline()
            {
#line (123, 5) - (123, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Indent("hello\n\nworld\n", "> ");
#line (124, 5) - (124, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("> hello\n\n> world\n", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndentEmptyString()
            {
#line (128, 5) - (128, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Indent("", "  ");
#line (129, 5) - (129, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestShortenFitsWithinWidth()
            {
#line (135, 5) - (135, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Shorten("Hello", 5);
#line (136, 5) - (136, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("Hello", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestShortenTruncatesWithPlaceholder()
            {
#line (140, 5) - (140, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Shorten("Hello World", 10);
#line (141, 5) - (141, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.True(result.Length <= 10);
#line (142, 5) - (142, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Contains("[...]", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestShortenCollapsesWhitespace()
            {
#line (146, 5) - (146, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Shorten("Hello  World", 11);
#line (147, 5) - (147, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("Hello World", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestShortenVerySmallWidthThrows()
            {
#line (151, 5) - (154, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (152, 9) - (152, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                    textwrap.Shorten("Hello World", 3);
#line hidden
                }
                catch (ValueError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestShortenCollapsedTextFitsExactly()
            {
#line (156, 5) - (156, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Shorten("Hello  World", 12);
#line (157, 5) - (157, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Equal("Hello World", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestShortenSingleLongWord()
            {
#line (161, 5) - (161, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                string result = textwrap.Shorten("Supercalifragilistic", 10);
#line (162, 5) - (162, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.Contains("[...]", result);
#line (163, 5) - (163, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/textwrap/textwrap_tests.spy"
                Xunit.Assert.True(result.Length <= 10);
#line hidden
            }
        }
    }
}
#line default
