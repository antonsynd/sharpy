// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using fnmatch = global::Sharpy.FnmatchModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Fnmatch.FnmatchTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Fnmatch
    {
        [global::Sharpy.SharpyModule("fnmatch.fnmatch_tests")]
        public static partial class FnmatchTests
        {
        }
    }

    public static partial class Fnmatch
    {
        public partial class FnmatchTestsTests
        {
            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute("foo.txt", "*.txt", true)]
            [Xunit.InlineDataAttribute("foo.py", "*.txt", false)]
            [Xunit.InlineDataAttribute("foo.TXT", "*.txt", false)]
            [Xunit.InlineDataAttribute("foo", "foo", true)]
            [Xunit.InlineDataAttribute("foo", "f?o", true)]
            [Xunit.InlineDataAttribute("fo", "f?o", false)]
            [Xunit.InlineDataAttribute("fooo", "f?o", false)]
            public void TestFnmatchcaseBasicPatterns(string name, string pat, bool expected)
            {
#line (16, 5) - (16, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal(expected, fnmatch.Fnmatchcase(name, pat));
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute("foo", "f[oa]o", true)]
            [Xunit.InlineDataAttribute("fbo", "f[oa]o", false)]
            public void TestFnmatchcaseCharacterClass(string name, string pat, bool expected)
            {
#line (24, 5) - (24, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal(expected, fnmatch.Fnmatchcase(name, pat));
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute("fxo", "f[!ab]o", true)]
            [Xunit.InlineDataAttribute("fao", "f[!ab]o", false)]
            [Xunit.InlineDataAttribute("fbo", "f[!ab]o", false)]
            public void TestFnmatchcaseNegatedCharacterClass(string name, string pat, bool expected)
            {
#line (33, 5) - (33, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal(expected, fnmatch.Fnmatchcase(name, pat));
            }

            [Xunit.FactAttribute]
            public void TestFnmatchcaseWildcardMatchesAnything()
            {
#line (37, 5) - (37, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.True(fnmatch.Fnmatchcase("anything", "*"));
#line (38, 5) - (38, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.True(fnmatch.Fnmatchcase("", "*"));
            }

            [Xunit.FactAttribute]
            public void TestFnmatchcaseQuestionMarkMatchesSingleChar()
            {
#line (42, 5) - (42, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.True(fnmatch.Fnmatchcase("a", "?"));
#line (43, 5) - (43, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.False(fnmatch.Fnmatchcase("", "?"));
#line (44, 5) - (44, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.False(fnmatch.Fnmatchcase("ab", "?"));
            }

            [Xunit.FactAttribute]
            public void TestFnmatchcaseSpecialCharsEscaped()
            {
#line (48, 5) - (48, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.True(fnmatch.Fnmatchcase("file.txt", "file.txt"));
#line (49, 5) - (49, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.False(fnmatch.Fnmatchcase("fileatxt", "file.txt"));
            }

            [Xunit.FactAttribute]
            public void TestFnmatchcaseCaseSensitive()
            {
#line (53, 5) - (53, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.False(fnmatch.Fnmatchcase("FOO.TXT", "*.txt"));
#line (54, 5) - (54, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.True(fnmatch.Fnmatchcase("FOO.TXT", "*.TXT"));
            }

            [Xunit.FactAttribute]
            public void TestFnmatchCaseSensitiveOnUnix()
            {
#line (61, 5) - (61, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.False(fnmatch.Fnmatch("FOO.TXT", "*.txt"));
            }

            [Xunit.FactAttribute]
            public void TestFnmatchBasicMatch()
            {
#line (65, 5) - (65, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.True(fnmatch.Fnmatch("foo.txt", "*.txt"));
#line (66, 5) - (66, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.False(fnmatch.Fnmatch("foo.py", "*.txt"));
            }

            [Xunit.FactAttribute]
            public void TestFilterReturnsMatchingNames()
            {
#line (72, 5) - (72, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Sharpy.List<string> names = new Sharpy.List<string>()
                {
                    "foo.txt",
                    "bar.py",
                    "baz.txt"
                };
#line (73, 5) - (73, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Sharpy.List<string> result = fnmatch.Filter(names, "*.txt");
#line (74, 5) - (74, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line (75, 5) - (75, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal("foo.txt", result[0]);
#line (76, 5) - (76, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal("baz.txt", result[1]);
            }

            [Xunit.FactAttribute]
            public void TestFilterNoMatchesReturnsEmptyList()
            {
#line (80, 5) - (80, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Sharpy.List<string> names = new Sharpy.List<string>()
                {
                    "foo.py",
                    "bar.py"
                };
#line (81, 5) - (81, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Sharpy.List<string> result = fnmatch.Filter(names, "*.txt");
#line (82, 5) - (82, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestFilterEmptyListReturnsEmptyList()
            {
#line (86, 5) - (86, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Sharpy.List<string> names = new Sharpy.List<string>()
                {
                };
#line (87, 5) - (87, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Sharpy.List<string> result = fnmatch.Filter(names, "*");
#line (88, 5) - (88, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestTranslateStarToRegex()
            {
#line (94, 5) - (94, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                string result = fnmatch.Translate("*.txt");
#line (95, 5) - (95, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal("\\A(?s:.*\\.txt)\\Z", result);
            }

            [Xunit.FactAttribute]
            public void TestTranslateQuestionMarkToRegex()
            {
#line (99, 5) - (99, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                string result = fnmatch.Translate("?.txt");
#line (100, 5) - (100, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal("\\A(?s:.\\.txt)\\Z", result);
            }

            [Xunit.FactAttribute]
            public void TestTranslateCharacterClassToRegex()
            {
#line (104, 5) - (104, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                string result = fnmatch.Translate("[abc]");
#line (105, 5) - (105, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal("\\A(?s:[abc])\\Z", result);
            }

            [Xunit.FactAttribute]
            public void TestTranslateNegatedClassToRegex()
            {
#line (109, 5) - (109, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                string result = fnmatch.Translate("[!abc]");
#line (110, 5) - (110, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal("\\A(?s:[^abc])\\Z", result);
            }

            [Xunit.FactAttribute]
            public void TestTranslateUnclosedBracketTreatedAsLiteral()
            {
#line (114, 5) - (114, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                string result = fnmatch.Translate("[abc");
#line (115, 5) - (115, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Contains("\\[", result);
            }

            [Xunit.FactAttribute]
            public void TestTranslateStarOnly()
            {
#line (119, 5) - (119, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                string result = fnmatch.Translate("*");
#line (120, 5) - (120, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/fnmatch/fnmatch_tests.spy"
                Xunit.Assert.Equal("\\A(?s:.*)\\Z", result);
            }
        }
    }
}
