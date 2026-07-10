// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using textwrap = global::Sharpy.Textwrap;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Cpython.CpythonTextwrapTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Cpython
    {
        [global::Sharpy.SharpyModule("cpython.cpython_textwrap_tests")]
        public static partial class CpythonTextwrapTests
        {
        }
    }

    public static partial class Cpython
    {
        public partial class CpythonTextwrapTestsTests
        {
            [Xunit.FactAttribute]
            public void TestWrapShort()
            {
#line (28, 5) - (28, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "This is a\nshort paragraph.";
#line (29, 5) - (29, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "This is a short", "paragraph." }, textwrap.Wrap(text, 20));
#line (30, 5) - (30, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "This is a short paragraph." }, textwrap.Wrap(text, 40));
            }

            [Xunit.FactAttribute]
            public void TestWrapShort1line()
            {
#line (35, 5) - (35, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "This is a short line.";
#line (36, 5) - (36, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "This is a short line." }, textwrap.Wrap(text, 30));
            }

            [Xunit.FactAttribute]
            public void TestWrapEmptyString()
            {
#line (41, 5) - (41, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(textwrap.Wrap("", 6)));
            }

            [Xunit.FactAttribute]
            public void TestDedentNomargin()
            {
#line (48, 5) - (48, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "Hello there.\nHow are you?\nOh good, I'm glad.";
#line (49, 5) - (49, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(text, textwrap.Dedent(text));
#line (50, 5) - (50, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "Hello there.\n\nBoo!";
#line (51, 5) - (51, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(text, textwrap.Dedent(text));
#line (52, 5) - (52, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "Hello there.\n  This is indented.";
#line (53, 5) - (53, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(text, textwrap.Dedent(text));
#line (54, 5) - (54, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "Hello there.\n\n  Boo!\n";
#line (55, 5) - (55, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(text, textwrap.Dedent(text));
            }

            [Xunit.FactAttribute]
            public void TestDedentEven()
            {
#line (60, 5) - (60, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "  Hello there.\n  How are ya?\n  Oh good.";
#line (61, 5) - (61, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("Hello there.\nHow are ya?\nOh good.", textwrap.Dedent(text));
#line (62, 5) - (62, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "  Hello there.\n\n  How are ya?\n  Oh good.\n";
#line (63, 5) - (63, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("Hello there.\n\nHow are ya?\nOh good.\n", textwrap.Dedent(text));
#line (64, 5) - (64, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "  Hello there.\n  \n  How are ya?\n  Oh good.\n";
#line (65, 5) - (65, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("Hello there.\n\nHow are ya?\nOh good.\n", textwrap.Dedent(text));
            }

            [Xunit.FactAttribute]
            public void TestDedentUneven()
            {
#line (70, 5) - (70, 98) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "        def foo():\n            while 1:\n                return foo\n        ";
#line (71, 5) - (71, 86) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("def foo():\n    while 1:\n        return foo\n", textwrap.Dedent(text));
#line (72, 5) - (72, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "  Foo\n    Bar\n\n   Baz\n";
#line (73, 5) - (73, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("Foo\n  Bar\n\n Baz\n", textwrap.Dedent(text));
#line (74, 5) - (74, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "  Foo\n    Bar\n \n   Baz\n";
#line (75, 5) - (75, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("Foo\n  Bar\n\n Baz\n", textwrap.Dedent(text));
            }

            [Xunit.FactAttribute]
            public void TestDedentDeclining()
            {
#line (80, 5) - (80, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "     Foo\n    Bar\n";
#line (81, 5) - (81, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(" Foo\nBar\n", textwrap.Dedent(text));
#line (82, 5) - (82, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "     Foo\n\n    Bar\n";
#line (83, 5) - (83, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(" Foo\n\nBar\n", textwrap.Dedent(text));
#line (84, 5) - (84, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "     Foo\n    \n    Bar\n";
#line (85, 5) - (85, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(" Foo\n\nBar\n", textwrap.Dedent(text));
            }

            [Xunit.FactAttribute]
            public void TestDedentPreserveInternalTabs()
            {
#line (90, 5) - (90, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "  hello\tthere\n  how are\tyou?";
#line (91, 5) - (91, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string expect = "hello\tthere\nhow are\tyou?";
#line (92, 5) - (92, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(expect, textwrap.Dedent(text));
#line (93, 5) - (93, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(expect, textwrap.Dedent(expect));
            }

            [Xunit.FactAttribute]
            public void TestDedentPreserveMarginTabs()
            {
#line (98, 5) - (98, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("  hello there\n\thow are you?", textwrap.Dedent("  hello there\n\thow are you?"));
#line (99, 5) - (99, 108) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("        hello there\n\thow are you?", textwrap.Dedent("        hello there\n\thow are you?"));
#line (100, 5) - (100, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "\thello there\n\thow are you?";
#line (101, 5) - (101, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string expect = "hello there\nhow are you?";
#line (102, 5) - (102, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(expect, textwrap.Dedent(text));
#line (103, 5) - (103, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "  \thello there\n  \thow are you?";
#line (104, 5) - (104, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(expect, textwrap.Dedent(text));
#line (105, 5) - (105, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "  \t  hello there\n  \t  how are you?";
#line (106, 5) - (106, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(expect, textwrap.Dedent(text));
#line (107, 5) - (107, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "  \thello there\n  \t  how are you?";
#line (108, 5) - (108, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("hello there\n  how are you?", textwrap.Dedent(text));
#line (109, 5) - (109, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "  \thello there\n   \thow are you?\n \tI'm fine, thanks";
#line (110, 5) - (110, 92) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(" \thello there\n  \thow are you?\n\tI'm fine, thanks", textwrap.Dedent(text));
            }

            [Xunit.FactAttribute]
            public void TestIndentNomarginDefault()
            {
#line (117, 5) - (117, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "Hi.\nThis is a test.\nTesting.";
#line (118, 5) - (118, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(text, textwrap.Indent(text, ""));
#line (119, 5) - (119, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "Hi.\nThis is a test.\n\nTesting.";
#line (120, 5) - (120, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(text, textwrap.Indent(text, ""));
#line (121, 5) - (121, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                text = "\nHi.\nThis is a test.\nTesting.\n";
#line (122, 5) - (122, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(text, textwrap.Indent(text, ""));
            }

            [Xunit.FactAttribute]
            public void TestIndentDefault()
            {
#line (128, 5) - (128, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string prefix = "  ";
#line (129, 5) - (129, 112) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("  Hi.\n  This is a test.\n  Testing.", textwrap.Indent("Hi.\nThis is a test.\nTesting.", prefix));
#line (130, 5) - (130, 116) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("  Hi.\n  This is a test.\n\n  Testing.", textwrap.Indent("Hi.\nThis is a test.\n\nTesting.", prefix));
#line (131, 5) - (131, 120) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("\n  Hi.\n  This is a test.\n  Testing.\n", textwrap.Indent("\nHi.\nThis is a test.\nTesting.\n", prefix));
            }

            [Xunit.FactAttribute]
            public void TestShortenSimple()
            {
#line (138, 5) - (138, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                string text = "Hello there, how are you this fine day? I'm glad to hear it!";
#line (139, 5) - (139, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("Hello there, [...]", textwrap.Shorten(text, 18));
#line (140, 5) - (140, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal(text, textwrap.Shorten(text, text.Length));
#line (141, 5) - (141, 113) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("Hello there, how are you this fine day? I'm glad to [...]", textwrap.Shorten(text, text.Length - 1));
            }

            [Xunit.FactAttribute]
            public void TestShortenEmptyString()
            {
#line (146, 5) - (146, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("", textwrap.Shorten("", 6));
            }

            [Xunit.FactAttribute]
            public void TestShortenWhitespace()
            {
#line (152, 5) - (152, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("hello world!", textwrap.Shorten("hello      world!  ", 12));
#line (153, 5) - (153, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("hello [...]", textwrap.Shorten("hello      world!  ", 11));
            }

            [Xunit.FactAttribute]
            public void TestShortenFirstWordTooLongButPlaceholderFits()
            {
#line (158, 5) - (158, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_textwrap_tests.spy"
                Xunit.Assert.Equal("[...]", textwrap.Shorten("Helloo", 5));
            }
        }
    }
}
