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
using shlex = global::Sharpy.ShlexModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Shlex.ShlexModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Shlex
    {
        [global::Sharpy.SharpyModule("shlex.shlex_module_tests")]
        public static partial class ShlexModuleTests
        {
        }
    }

    public static partial class Shlex
    {
        public partial class ShlexModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSplitSimpleWords()
            {
#line (9, 5) - (9, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "hello", "world" }, shlex.Split("hello world"));
            }

            [Xunit.FactAttribute]
            public void TestSplitSingleQuotedString()
            {
#line (13, 5) - (13, 73) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "echo", "hello world" }, shlex.Split("echo 'hello world'"));
            }

            [Xunit.FactAttribute]
            public void TestSplitDoubleQuotedString()
            {
#line (17, 5) - (17, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> result = shlex.Split("echo \"hello world\"");
#line (18, 5) - (18, 46) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "echo", "hello world" }, result);
            }

            [Xunit.FactAttribute]
            public void TestSplitBackslashEscaping()
            {
#line (22, 5) - (22, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "hello world" }, shlex.Split("hello\\ world"));
            }

            [Xunit.FactAttribute]
            public void TestSplitBackslashInDoubleQuotes()
            {
#line (26, 5) - (26, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> result = shlex.Split("\"hello\\\"world\"");
#line (27, 5) - (27, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "hello\"world" }, result);
            }

            [Xunit.FactAttribute]
            public void TestSplitPipesAndRedirects()
            {
#line (31, 5) - (31, 81) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> result = shlex.Split("cat file.txt | grep pattern > output.txt");
#line (32, 5) - (32, 85) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "cat", "file.txt", "|", "grep", "pattern", ">", "output.txt" }, result);
            }

            [Xunit.FactAttribute]
            public void TestSplitEmptyString()
            {
#line (36, 5) - (36, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(shlex.Split("")));
            }

            [Xunit.FactAttribute]
            public void TestSplitWhitespaceOnly()
            {
#line (40, 5) - (40, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(shlex.Split("   \t  \n  ")));
            }

            [Xunit.FactAttribute]
            public void TestSplitMultipleSpaces()
            {
#line (44, 5) - (44, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, shlex.Split("a   b     c"));
            }

            [Xunit.FactAttribute]
            public void TestSplitUnicodeInQuotes()
            {
#line (48, 5) - (48, 54) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> result = shlex.Split("echo '日本語テスト'");
#line (49, 5) - (49, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "echo", "日本語テスト" }, result);
            }

            [Xunit.FactAttribute]
            public void TestSplitAdjacentQuotedStrings()
            {
#line (53, 5) - (53, 62) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "hello world" }, shlex.Split("'hello ''world'"));
            }

            [Xunit.FactAttribute]
            public void TestSplitMixedQuotes()
            {
#line (57, 5) - (57, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> result = shlex.Split("echo 'single' \"double\"");
#line (58, 5) - (58, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "echo", "single", "double" }, result);
            }

            [Xunit.FactAttribute]
            public void TestSplitHashNotCommentByDefault()
            {
#line (62, 5) - (62, 53) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> result = shlex.Split("echo foo#bar");
#line (63, 5) - (63, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "echo", "foo#bar" }, result);
            }

            [Xunit.FactAttribute]
            public void TestSplitHashAsCommentWhenEnabled()
            {
#line (67, 5) - (67, 73) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> result = shlex.Split("echo foo #comment", comments: true);
#line (68, 5) - (68, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "echo", "foo" }, result);
            }

            [Xunit.FactAttribute]
            public void TestSplitHashInsideQuotesNotComment()
            {
#line (72, 5) - (72, 70) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> result = shlex.Split("echo 'foo#bar'", comments: true);
#line (73, 5) - (73, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "echo", "foo#bar" }, result);
            }

            [Xunit.FactAttribute]
            public void TestSplitUnclosedSingleQuoteThrows()
            {
#line (77, 5) - (80, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (78, 9) - (78, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                    shlex.Split("echo 'hello");
                }));
            }

            [Xunit.FactAttribute]
            public void TestSplitUnclosedDoubleQuoteThrows()
            {
#line (82, 5) - (85, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (83, 9) - (83, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                    shlex.Split("echo \"hello");
                }));
            }

            [Xunit.FactAttribute]
            public void TestSplitTrailingBackslashThrows()
            {
#line (87, 5) - (90, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (88, 9) - (88, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                    shlex.Split("echo test\\");
                }));
            }

            [Xunit.FactAttribute]
            public void TestSplitNonPosixModeThrows()
            {
#line (92, 5) - (95, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (93, 9) - (93, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                    shlex.Split("test", posix: false);
                }));
            }

            [Xunit.FactAttribute]
            public void TestSplitTabAndNewlineAsDelimiters()
            {
#line (97, 5) - (97, 54) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, shlex.Split("a\tb\nc"));
            }

            [Xunit.FactAttribute]
            public void TestSplitNonSpecialBackslashInDoubleQuotes()
            {
#line (101, 5) - (101, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> result = shlex.Split("\"hello\\nworld\"");
#line (102, 5) - (102, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "hello\\nworld" }, result);
            }

            [Xunit.FactAttribute]
            public void TestQuoteSafeStringNoQuoting()
            {
#line (108, 5) - (108, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal("hello", shlex.Quote("hello"));
            }

            [Xunit.FactAttribute]
            public void TestQuoteEmptyString()
            {
#line (112, 5) - (112, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal("''", shlex.Quote(""));
            }

            [Xunit.FactAttribute]
            public void TestQuoteStringWithSpaces()
            {
#line (116, 5) - (116, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal("'hello world'", shlex.Quote("hello world"));
            }

            [Xunit.FactAttribute]
            public void TestQuoteStringWithSingleQuote()
            {
#line (120, 5) - (120, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal("'it'\"'\"'s'", shlex.Quote("it's"));
            }

            [Xunit.FactAttribute]
            public void TestQuoteStringWithSpecialChars()
            {
#line (124, 5) - (124, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal("'hello;world'", shlex.Quote("hello;world"));
            }

            [Xunit.FactAttribute]
            public void TestQuoteSafeCharsNotQuoted()
            {
#line (128, 5) - (128, 72) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal("file-name_v2.0/path", shlex.Quote("file-name_v2.0/path"));
            }

            [Xunit.FactAttribute]
            public void TestJoinSimpleTokens()
            {
#line (134, 5) - (134, 72) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal("echo 'hello world'", shlex.Join(new Sharpy.List<string>() { "echo", "hello world" }));
            }

            [Xunit.FactAttribute]
            public void TestJoinEmptyList()
            {
#line (138, 5) - (138, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> tokens = new Sharpy.List<string>()
                {
                };
#line (139, 5) - (139, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal("", shlex.Join(tokens));
            }

            [Xunit.FactAttribute]
            public void TestJoinSingleToken()
            {
#line (143, 5) - (143, 45) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal("hello", shlex.Join(new Sharpy.List<string>() { "hello" }));
            }

            [Xunit.FactAttribute]
            public void TestSplitJoinRoundtrip()
            {
#line (149, 5) - (149, 71) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> tokens = new Sharpy.List<string>()
                {
                    "echo",
                    "hello world",
                    "|",
                    "grep",
                    "hello"
                };
#line (150, 5) - (150, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                string joined = shlex.Join(tokens);
#line (151, 5) - (151, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Sharpy.List<string> splitResult = shlex.Split(joined);
#line (152, 5) - (152, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/shlex/shlex_module_tests.spy"
                Xunit.Assert.Equal(tokens, splitResult);
            }
        }
    }
}
