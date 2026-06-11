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
#line (9, 5) - (9, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(5, grapheme.Length("hello"));
            }

            [Xunit.FactAttribute]
            public void TestLengthEmptyStringReturnsZero()
            {
#line (13, 5) - (13, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(0, grapheme.Length(""));
            }

            [Xunit.FactAttribute]
            public void TestLengthCombiningAccentTreatedAsSingleGrapheme()
            {
#line (18, 5) - (18, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(1, grapheme.Length("é"));
            }

            [Xunit.FactAttribute]
            public void TestLengthPrecomposedAccentIsSingleGrapheme()
            {
#line (23, 5) - (23, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(1, grapheme.Length("é"));
            }

            [Xunit.FactAttribute]
            public void TestLengthSurrogatePairEmojiIsSingleGrapheme()
            {
#line (28, 5) - (28, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(1, grapheme.Length("😀"));
            }

            [Xunit.FactAttribute]
            public void TestLengthZwjFamilySequenceIsSingleGrapheme()
            {
#line (32, 5) - (32, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                string family = "👨‍👩‍👧‍👦";
#line (33, 5) - (33, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(1, grapheme.Length(family));
            }

            [Xunit.FactAttribute]
            public void TestLengthMixedAsciiAndEmojiCountsCorrectly()
            {
#line (37, 5) - (37, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(4, grapheme.Length("hi😀!"));
            }

            [Xunit.FactAttribute]
            public void TestGraphemesAsciiSplitsByCharacter()
            {
#line (43, 5) - (43, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, grapheme.Graphemes("abc"));
            }

            [Xunit.FactAttribute]
            public void TestGraphemesEmptyStringReturnsEmpty()
            {
#line (47, 5) - (47, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(grapheme.Graphemes("")));
            }

            [Xunit.FactAttribute]
            public void TestGraphemesCombiningAccentKeepsTogether()
            {
#line (51, 5) - (51, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                var result = grapheme.Graphemes("éa");
#line (52, 5) - (52, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "é", "a" }, result);
            }

            [Xunit.FactAttribute]
            public void TestGraphemesEmojiKeepsSurrogatePairTogether()
            {
#line (56, 5) - (56, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                var result = grapheme.Graphemes("a😀b");
#line (57, 5) - (57, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "😀", "b" }, result);
            }

            [Xunit.FactAttribute]
            public void TestAtPositiveIndexReturnsGrapheme()
            {
#line (63, 5) - (63, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("a", grapheme.At("abc", 0));
#line (64, 5) - (64, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("b", grapheme.At("abc", 1));
#line (65, 5) - (65, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("c", grapheme.At("abc", 2));
            }

            [Xunit.FactAttribute]
            public void TestAtNegativeIndexCountsFromEnd()
            {
#line (69, 5) - (69, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("c", grapheme.At("abc", -1));
#line (70, 5) - (70, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("b", grapheme.At("abc", -2));
#line (71, 5) - (71, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("a", grapheme.At("abc", -3));
            }

            [Xunit.FactAttribute]
            public void TestAtEmojiReturnsWholeGrapheme()
            {
#line (75, 5) - (75, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("😀", grapheme.At("a😀b", 1));
            }

            [Xunit.FactAttribute]
            public void TestAtIndexEqualsLengthThrowsIndexError()
            {
#line (79, 5) - (82, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (80, 9) - (80, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                    grapheme.At("abc", 3);
                }));
            }

            [Xunit.FactAttribute]
            public void TestAtNegativeBeyondStartThrowsIndexError()
            {
#line (84, 5) - (87, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (85, 9) - (85, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                    grapheme.At("abc", -4);
                }));
            }

            [Xunit.FactAttribute]
            public void TestAtEmptyStringThrowsIndexError()
            {
#line (89, 5) - (94, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (90, 9) - (90, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                    grapheme.At("", 0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestSliceFullRangeReturnsAll()
            {
#line (96, 5) - (96, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("hello", grapheme.Slice("hello", 0, 5));
            }

            [Xunit.FactAttribute]
            public void TestSliceMidRangeReturnsSubstring()
            {
#line (100, 5) - (100, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("ell", grapheme.Slice("hello", 1, 4));
            }

            [Xunit.FactAttribute]
            public void TestSliceOpenEndReturnsToEnd()
            {
#line (104, 5) - (104, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("llo", grapheme.Slice("hello", 2));
            }

            [Xunit.FactAttribute]
            public void TestSliceEmptyStringReturnsEmpty()
            {
#line (108, 5) - (108, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("", grapheme.Slice("", 0, 5));
            }

            [Xunit.FactAttribute]
            public void TestSliceStartAtEndReturnsEmpty()
            {
#line (112, 5) - (112, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("", grapheme.Slice("hello", 5, 5));
            }

            [Xunit.FactAttribute]
            public void TestSliceNegativeStartCountsFromEnd()
            {
#line (116, 5) - (116, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("llo", grapheme.Slice("hello", -3));
            }

            [Xunit.FactAttribute]
            public void TestSliceNegativeEndCountsFromEnd()
            {
#line (120, 5) - (120, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("hel", grapheme.Slice("hello", 0, -2));
            }

            [Xunit.FactAttribute]
            public void TestSliceStartGreaterThanEndReturnsEmpty()
            {
#line (124, 5) - (124, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("", grapheme.Slice("hello", 3, 1));
            }

            [Xunit.FactAttribute]
            public void TestSliceOutOfRangeClamped()
            {
#line (128, 5) - (128, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("hi", grapheme.Slice("hi", 0, 100));
            }

            [Xunit.FactAttribute]
            public void TestSliceAcrossEmojiKeepsClustersWhole()
            {
#line (132, 5) - (132, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                string s = "a😀b😀c";
#line (133, 5) - (133, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/grapheme/grapheme_tests.spy"
                Xunit.Assert.Equal("😀b😀", grapheme.Slice(s, 1, 4));
            }
        }
    }
}
