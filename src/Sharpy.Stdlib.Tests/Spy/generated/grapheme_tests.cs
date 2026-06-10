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
using grapheme = global::Sharpy.Grapheme;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Grapheme.GraphemeTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Grapheme
    {
        [global::Sharpy.SharpyModule("grapheme.grapheme_tests")]
        public static partial class GraphemeTests
        {
        }
    }

    public static partial class Grapheme
    {
        public partial class GraphemeTestsTests
        {
            [Xunit.FactAttribute]
            public void TestLengthAsciiReturnsCharacterCount()
            {
#line (9, 5) - (9, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(5, grapheme.Length("hello"));
            }

            [Xunit.FactAttribute]
            public void TestLengthEmptyStringReturnsZero()
            {
#line (13, 5) - (13, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(0, grapheme.Length(""));
            }

            [Xunit.FactAttribute]
            public void TestLengthCombiningAccentTreatedAsSingleGrapheme()
            {
#line (18, 5) - (18, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(1, grapheme.Length("é"));
            }

            [Xunit.FactAttribute]
            public void TestLengthPrecomposedAccentIsSingleGrapheme()
            {
#line (23, 5) - (23, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(1, grapheme.Length("é"));
            }

            [Xunit.FactAttribute]
            public void TestLengthSurrogatePairEmojiIsSingleGrapheme()
            {
#line (28, 5) - (28, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(1, grapheme.Length(""));
            }

            [Xunit.FactAttribute]
            public void TestLengthMixedAsciiAndEmojiCountsCorrectly()
            {
#line (38, 5) - (38, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(4, grapheme.Length("hi!"));
            }

            [Xunit.FactAttribute]
            public void TestGraphemesAsciiSplitsByCharacter()
            {
#line (44, 5) - (44, 57) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, grapheme.Graphemes("abc"));
            }

            [Xunit.FactAttribute]
            public void TestGraphemesEmptyStringReturnsEmpty()
            {
#line (48, 5) - (48, 45) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(grapheme.Graphemes("")));
            }

            [Xunit.FactAttribute]
            public void TestGraphemesCombiningAccentKeepsTogether()
            {
#line (52, 5) - (52, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                var result = grapheme.Graphemes("éa");
#line (53, 5) - (53, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "é", "a" }, result);
            }

            [Xunit.FactAttribute]
            public void TestGraphemesEmojiKeepsSurrogatePairTogether()
            {
#line (57, 5) - (57, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                var result = grapheme.Graphemes("ab");
#line (58, 5) - (58, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "", "b" }, result);
            }

            [Xunit.FactAttribute]
            public void TestAtPositiveIndexReturnsGrapheme()
            {
#line (64, 5) - (64, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("a", grapheme.At("abc", 0));
#line (65, 5) - (65, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("b", grapheme.At("abc", 1));
#line (66, 5) - (66, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("c", grapheme.At("abc", 2));
            }

            [Xunit.FactAttribute]
            public void TestAtNegativeIndexCountsFromEnd()
            {
#line (70, 5) - (70, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("c", grapheme.At("abc", -1));
#line (71, 5) - (71, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("b", grapheme.At("abc", -2));
#line (72, 5) - (72, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("a", grapheme.At("abc", -3));
            }

            [Xunit.FactAttribute]
            public void TestAtEmojiReturnsWholeGrapheme()
            {
#line (76, 5) - (76, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("", grapheme.At("ab", 1));
            }

            [Xunit.FactAttribute]
            public void TestAtIndexEqualsLengthThrowsIndexError()
            {
#line (80, 5) - (83, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (81, 9) - (81, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                    grapheme.At("abc", 3);
                }));
            }

            [Xunit.FactAttribute]
            public void TestAtNegativeBeyondStartThrowsIndexError()
            {
#line (85, 5) - (88, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (86, 9) - (86, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                    grapheme.At("abc", -4);
                }));
            }

            [Xunit.FactAttribute]
            public void TestAtEmptyStringThrowsIndexError()
            {
#line (90, 5) - (95, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (91, 9) - (91, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                    grapheme.At("", 0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestSliceFullRangeReturnsAll()
            {
#line (97, 5) - (97, 53) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("hello", grapheme.Slice("hello", 0, 5));
            }

            [Xunit.FactAttribute]
            public void TestSliceMidRangeReturnsSubstring()
            {
#line (101, 5) - (101, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("ell", grapheme.Slice("hello", 1, 4));
            }

            [Xunit.FactAttribute]
            public void TestSliceOpenEndReturnsToEnd()
            {
#line (105, 5) - (105, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("llo", grapheme.Slice("hello", 2));
            }

            [Xunit.FactAttribute]
            public void TestSliceEmptyStringReturnsEmpty()
            {
#line (109, 5) - (109, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("", grapheme.Slice("", 0, 5));
            }

            [Xunit.FactAttribute]
            public void TestSliceStartAtEndReturnsEmpty()
            {
#line (113, 5) - (113, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("", grapheme.Slice("hello", 5, 5));
            }

            [Xunit.FactAttribute]
            public void TestSliceNegativeStartCountsFromEnd()
            {
#line (117, 5) - (117, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("llo", grapheme.Slice("hello", -3));
            }

            [Xunit.FactAttribute]
            public void TestSliceNegativeEndCountsFromEnd()
            {
#line (121, 5) - (121, 52) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("hel", grapheme.Slice("hello", 0, -2));
            }

            [Xunit.FactAttribute]
            public void TestSliceStartGreaterThanEndReturnsEmpty()
            {
#line (125, 5) - (125, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("", grapheme.Slice("hello", 3, 1));
            }

            [Xunit.FactAttribute]
            public void TestSliceOutOfRangeClamped()
            {
#line (129, 5) - (129, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("hi", grapheme.Slice("hi", 0, 100));
            }

            [Xunit.FactAttribute]
            public void TestSliceAcrossEmojiKeepsClustersWhole()
            {
#line (133, 5) - (133, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                string s = "abc";
#line (134, 5) - (134, 63) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("b", grapheme.Slice(s, 1, 4));
            }
        }
    }
}
