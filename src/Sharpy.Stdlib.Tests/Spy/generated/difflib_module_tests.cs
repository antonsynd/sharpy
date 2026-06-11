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
using difflib = global::Sharpy.DifflibModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Difflib.DifflibModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Difflib
    {
        [global::Sharpy.SharpyModule("difflib.difflib_module_tests")]
        public static partial class DifflibModuleTests
        {
        }
    }

    public static partial class Difflib
    {
        public partial class DifflibModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSequenceMatcherRatioMatchesPython()
            {
#line (9, 5) - (9, 98) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, new Sharpy.List<string>() { "a", "b", "c", "d", "e" }, new Sharpy.List<string>() { "a", "b", "d", "c", "e" });
#line (10, 5) - (10, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(0.8d, sm.Ratio(), 0.001d);
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherGetMatchingBlocksReturnsCorrectBlocks()
            {
#line (14, 5) - (14, 98) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, new Sharpy.List<string>() { "a", "b", "c", "d", "e" }, new Sharpy.List<string>() { "a", "b", "d", "c", "e" });
#line (15, 5) - (15, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var blocks = sm.GetMatchingBlocks();
#line (16, 5) - (16, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var last = blocks[global::Sharpy.Builtins.Len(blocks) - 1];
#line (17, 5) - (17, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal((5, 5, 0), last);
#line (18, 5) - (18, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                int totalSize = 0;
#line (19, 5) - (21, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                foreach (var __loopVar_0 in blocks)
                {
                    var b = __loopVar_0;
#line (20, 9) - (20, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    totalSize = totalSize + b.Item3;
                }

#line (21, 5) - (21, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(4, totalSize);
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherGetOpcodesReturnsCorrectTags()
            {
#line (25, 5) - (25, 98) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, new Sharpy.List<string>() { "a", "b", "c", "d", "e" }, new Sharpy.List<string>() { "a", "b", "d", "c", "e" });
#line (26, 5) - (26, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var opcodes = sm.GetOpcodes();
#line (27, 5) - (27, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasEqual = false;
#line (28, 5) - (28, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasChange = false;
#line (29, 5) - (34, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                foreach (var __loopVar_1 in opcodes)
                {
                    var op = __loopVar_1;
#line (30, 9) - (32, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (op.Item1 == "equal")
                    {
#line (31, 13) - (31, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasEqual = true;
                    }

#line (32, 9) - (34, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (op.Item1 == "insert" || op.Item1 == "delete" || op.Item1 == "replace")
                    {
#line (33, 13) - (33, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasChange = true;
                    }
                }

#line (34, 5) - (34, 22) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasEqual);
#line (35, 5) - (35, 23) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasChange);
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherQuickRatioIsUpperBound()
            {
#line (39, 5) - (39, 98) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, new Sharpy.List<string>() { "a", "b", "c", "d", "e" }, new Sharpy.List<string>() { "a", "b", "d", "c", "e" });
#line (40, 5) - (40, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(sm.QuickRatio() >= sm.Ratio());
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherRealQuickRatioIsUpperBound()
            {
#line (44, 5) - (44, 98) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, new Sharpy.List<string>() { "a", "b", "c", "d", "e" }, new Sharpy.List<string>() { "a", "b", "d", "c", "e" });
#line (45, 5) - (45, 54) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(sm.RealQuickRatio() >= sm.QuickRatio());
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherEmptySequencesRatioIsOne()
            {
#line (49, 5) - (49, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
                {
                };
#line (50, 5) - (50, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, empty, empty);
#line (51, 5) - (51, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(1.0d, sm.Ratio());
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherOneEmptyRatioIsZero()
            {
#line (55, 5) - (55, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
                {
                };
#line (56, 5) - (56, 68) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, new Sharpy.List<string>() { "a", "b", "c" }, empty);
#line (57, 5) - (57, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(0.0d, sm.Ratio());
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherJunkFunctionExcludesElements()
            {
#line (61, 5) - (61, 122) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(c => c == " ", new Sharpy.List<string>() { "a", " ", "b", " ", "c" }, new Sharpy.List<string>() { "a", " ", " ", "b", " ", " ", "c" });
#line (62, 5) - (62, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(sm.Ratio() > 0.5d);
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherSetSeqsResetsState()
            {
#line (66, 5) - (66, 78) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, new Sharpy.List<string>() { "a", "b", "c" }, new Sharpy.List<string>() { "a", "b", "c" });
#line (67, 5) - (67, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(1.0d, sm.Ratio());
#line (68, 5) - (68, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                sm.SetSeqs(new Sharpy.List<string>() { "a", "b", "c" }, new Sharpy.List<string>() { "x", "y", "z" });
#line (69, 5) - (69, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(sm.Ratio() < 1.0d);
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherIdenticalSequencesRatioIsOne()
            {
#line (73, 5) - (73, 92) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, new Sharpy.List<string>() { "line1\n", "line2\n" }, new Sharpy.List<string>() { "line1\n", "line2\n" });
#line (74, 5) - (74, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(1.0d, sm.Ratio());
            }

            [Xunit.FactAttribute]
            public void TestGetCloseMatchesMatchesPython()
            {
#line (80, 5) - (80, 75) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var result = difflib.GetCloseMatches("appel", new Sharpy.List<string>() { "ape", "apple", "peach" });
#line (81, 5) - (81, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "apple", "ape" }, result);
            }

            [Xunit.FactAttribute]
            public void TestGetCloseMatchesHighCutoffFiltersWorse()
            {
#line (85, 5) - (85, 87) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var result = difflib.GetCloseMatches("appel", new Sharpy.List<string>() { "ape", "apple", "peach" }, cutoff: 0.9d);
#line (86, 5) - (86, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.DoesNotContain("ape", result);
            }

            [Xunit.FactAttribute]
            public void TestGetCloseMatchesN1ReturnsBestOnly()
            {
#line (90, 5) - (90, 80) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var result = difflib.GetCloseMatches("appel", new Sharpy.List<string>() { "ape", "apple", "peach" }, n: 1);
#line (91, 5) - (91, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (92, 5) - (92, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal("apple", result[0]);
            }

            [Xunit.FactAttribute]
            public void TestGetCloseMatchesNoMatchesReturnsEmpty()
            {
#line (96, 5) - (96, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var result = difflib.GetCloseMatches("xyz", new Sharpy.List<string>() { "abc", "def" }, cutoff: 0.9d);
#line (97, 5) - (97, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestIsLineJunkBlankLineReturnsTrue()
            {
#line (103, 5) - (103, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(difflib.IsLineJunk("  \n"));
            }

            [Xunit.FactAttribute]
            public void TestIsLineJunkCommentLineReturnsTrue()
            {
#line (107, 5) - (107, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(difflib.IsLineJunk("  #  \n"));
            }

            [Xunit.FactAttribute]
            public void TestIsLineJunkCodeLineReturnsFalse()
            {
#line (111, 5) - (111, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.False(difflib.IsLineJunk("code\n"));
            }

            [Xunit.FactAttribute]
            public void TestIsCharacterJunkSpaceReturnsTrue()
            {
#line (115, 5) - (115, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(difflib.IsCharacterJunk(" "));
            }

            [Xunit.FactAttribute]
            public void TestIsCharacterJunkTabReturnsTrue()
            {
#line (119, 5) - (119, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(difflib.IsCharacterJunk("\t"));
            }

            [Xunit.FactAttribute]
            public void TestIsCharacterJunkNewlineReturnsFalse()
            {
#line (123, 5) - (123, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.False(difflib.IsCharacterJunk("\n"));
            }

            [Xunit.FactAttribute]
            public void TestIsCharacterJunkLetterReturnsFalse()
            {
#line (127, 5) - (127, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.False(difflib.IsCharacterJunk("a"));
            }

            [Xunit.FactAttribute]
            public void TestUnifiedDiffBasicOutputHasCorrectFormat()
            {
#line (133, 5) - (133, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n",
                    "three\n"
                };
#line (134, 5) - (134, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
                {
                    "one\n",
                    "tree\n",
                    "three\n"
                };
#line (135, 5) - (135, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> diff = new global::Sharpy.List<string>(difflib.UnifiedDiff(a, b, "a.txt", "b.txt"));
#line (136, 5) - (136, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasFrom = false;
#line (137, 5) - (137, 26) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasTo = false;
#line (138, 5) - (138, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasHunk = false;
#line (139, 5) - (146, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                foreach (var __loopVar_2 in diff)
                {
                    var l = __loopVar_2;
#line (140, 9) - (142, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (l.Startswith("--- "))
                    {
#line (141, 13) - (141, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasFrom = true;
                    }

#line (142, 9) - (144, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (l.Startswith("+++ "))
                    {
#line (143, 13) - (143, 26) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasTo = true;
                    }

#line (144, 9) - (146, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (l.Startswith("@@ "))
                    {
#line (145, 13) - (145, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasHunk = true;
                    }
                }

#line (146, 5) - (146, 21) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasFrom);
#line (147, 5) - (147, 19) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasTo);
#line (148, 5) - (148, 21) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasHunk);
            }

            [Xunit.FactAttribute]
            public void TestUnifiedDiffIdenticalInputsNoOutput()
            {
#line (152, 5) - (152, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n"
                };
#line (153, 5) - (153, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> diff = new global::Sharpy.List<string>(difflib.UnifiedDiff(a, a));
#line (154, 5) - (154, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(diff));
            }

            [Xunit.FactAttribute]
            public void TestContextDiffHasCorrectFormat()
            {
#line (158, 5) - (158, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n",
                    "three\n"
                };
#line (159, 5) - (159, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
                {
                    "one\n",
                    "tree\n",
                    "three\n"
                };
#line (160, 5) - (160, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> diff = new global::Sharpy.List<string>(difflib.ContextDiff(a, b, "a.txt", "b.txt"));
#line (161, 5) - (161, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasStars = false;
#line (162, 5) - (162, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasDashes = false;
#line (163, 5) - (168, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                foreach (var __loopVar_3 in diff)
                {
                    var l = __loopVar_3;
#line (164, 9) - (166, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (l.Startswith("*** "))
                    {
#line (165, 13) - (165, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasStars = true;
                    }

#line (166, 9) - (168, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (l.Startswith("--- "))
                    {
#line (167, 13) - (167, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasDashes = true;
                    }
                }

#line (168, 5) - (168, 22) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasStars);
#line (169, 5) - (169, 23) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasDashes);
            }

            [Xunit.FactAttribute]
            public void TestNdiffCommonLinesPrefixedWithSpace()
            {
#line (175, 5) - (175, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n"
                };
#line (176, 5) - (176, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n"
                };
#line (177, 5) - (177, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> diff = new global::Sharpy.List<string>(difflib.Ndiff(a, b));
#line (178, 5) - (181, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                foreach (var __loopVar_4 in diff)
                {
                    var l = __loopVar_4;
#line (179, 9) - (179, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    Xunit.Assert.StartsWith("  ", l);
                }
            }

            [Xunit.FactAttribute]
            public void TestNdiffRemovedLinesPrefixedWithMinus()
            {
#line (183, 5) - (183, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n"
                };
#line (184, 5) - (184, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
                {
                    "one\n"
                };
#line (185, 5) - (185, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> diff = new global::Sharpy.List<string>(difflib.Ndiff(a, b));
#line (186, 5) - (186, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasMinus = false;
#line (187, 5) - (190, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                foreach (var __loopVar_5 in diff)
                {
                    var l = __loopVar_5;
#line (188, 9) - (190, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (l.Startswith("- "))
                    {
#line (189, 13) - (189, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasMinus = true;
                    }
                }

#line (190, 5) - (190, 22) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasMinus);
            }

            [Xunit.FactAttribute]
            public void TestNdiffAddedLinesPrefixedWithPlus()
            {
#line (194, 5) - (194, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "one\n"
                };
#line (195, 5) - (195, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n"
                };
#line (196, 5) - (196, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> diff = new global::Sharpy.List<string>(difflib.Ndiff(a, b));
#line (197, 5) - (197, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasPlus = false;
#line (198, 5) - (201, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                foreach (var __loopVar_6 in diff)
                {
                    var l = __loopVar_6;
#line (199, 9) - (201, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (l.Startswith("+ "))
                    {
#line (200, 13) - (200, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasPlus = true;
                    }
                }

#line (201, 5) - (201, 21) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasPlus);
            }

            [Xunit.FactAttribute]
            public void TestDifferCompareProducesDelta()
            {
#line (207, 5) - (207, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var d = new global::Sharpy.Differ();
#line (208, 5) - (208, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n",
                    "three\n"
                };
#line (209, 5) - (209, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
                {
                    "ore\n",
                    "tree\n",
                    "emu\n"
                };
#line (210, 5) - (210, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> result = new global::Sharpy.List<string>(d.Compare(a, b));
#line (211, 5) - (211, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(result) > 0);
            }

            [Xunit.FactAttribute]
            public void TestRestoreWhich1RecoversA()
            {
#line (217, 5) - (217, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n",
                    "three\n"
                };
#line (218, 5) - (218, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
                {
                    "ore\n",
                    "tree\n",
                    "emu\n"
                };
#line (219, 5) - (219, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> diff = new global::Sharpy.List<string>(difflib.Ndiff(a, b));
#line (220, 5) - (220, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> restored = new global::Sharpy.List<string>(difflib.Restore(diff, 1));
#line (221, 5) - (221, 26) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(a, restored);
            }

            [Xunit.FactAttribute]
            public void TestRestoreWhich2RecoversB()
            {
#line (225, 5) - (225, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "one\n",
                    "two\n",
                    "three\n"
                };
#line (226, 5) - (226, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
                {
                    "ore\n",
                    "tree\n",
                    "emu\n"
                };
#line (227, 5) - (227, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> diff = new global::Sharpy.List<string>(difflib.Ndiff(a, b));
#line (228, 5) - (228, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> restored = new global::Sharpy.List<string>(difflib.Restore(diff, 2));
#line (229, 5) - (229, 26) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(b, restored);
            }

            [Xunit.FactAttribute]
            public void TestRestoreInvalidWhichThrows()
            {
#line (233, 5) - (236, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (234, 9) - (234, 45) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    new global::Sharpy.List<string>(difflib.Restore(new Sharpy.List<string>() { "  line" }, 3));
                }));
            }

            [Xunit.FactAttribute]
            public void TestUnifiedDiffWithFileNamesInHeader()
            {
#line (238, 5) - (238, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                    "x\n"
                };
#line (239, 5) - (239, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
                {
                    "y\n"
                };
#line (240, 5) - (240, 78) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> diff = new global::Sharpy.List<string>(difflib.UnifiedDiff(a, b, "old.txt", "new.txt"));
#line (241, 5) - (241, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasOld = false;
#line (242, 5) - (242, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                bool hasNew = false;
#line (243, 5) - (248, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                foreach (var __loopVar_7 in diff)
                {
                    var l = __loopVar_7;
#line (244, 9) - (246, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (l.Contains("old.txt"))
                    {
#line (245, 13) - (245, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasOld = true;
                    }

#line (246, 9) - (248, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    if (l.Contains("new.txt"))
                    {
#line (247, 13) - (247, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                        hasNew = true;
                    }
                }

#line (248, 5) - (248, 20) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasOld);
#line (249, 5) - (249, 20) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(hasNew);
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherGetGroupedOpcodesGroupsByContext()
            {
#line (255, 5) - (255, 23) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
                {
                };
#line (256, 5) - (258, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                foreach (var __loopVar_8 in global::Sharpy.Builtins.Range(1, 41))
                {
                    var i = __loopVar_8;
#line (257, 9) - (257, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                    a.Append("line" + global::Sharpy.Builtins.Str(i) + "\n");
                }

#line (258, 5) - (258, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> b = new global::Sharpy.List<string>(a);
#line (259, 5) - (259, 23) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                b[8] = "changed\n";
#line (260, 5) - (260, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                b[20] = "also changed\n";
#line (261, 5) - (261, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, a, b);
#line (262, 5) - (262, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var groups = sm.GetGroupedOpcodes(3);
#line (263, 5) - (263, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(groups) >= 2);
            }

            [Xunit.FactAttribute]
            public void TestSequenceMatcherEmptyInputGetOpcodesIsEmpty()
            {
#line (267, 5) - (267, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
                {
                };
#line (268, 5) - (268, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                var sm = new global::Sharpy.SequenceMatcher<string>(null, empty, empty);
#line (269, 5) - (269, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/difflib/difflib_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(sm.GetOpcodes()));
            }
        }
    }
}
