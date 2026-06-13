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
using itertools = global::Sharpy.Itertools;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Itertools.ItertoolsTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Itertools
    {
        [global::Sharpy.SharpyModule("itertools.itertools_tests")]
        public static partial class ItertoolsTests
        {
        }
    }

    public static partial class Itertools
    {
        public partial class ItertoolsTestsTests
        {
            [Xunit.FactAttribute]
            public void TestChainConcatenatesMultipleIterables()
            {
#line (9, 5) - (9, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Chain(new Sharpy.List<int>() { 1, 2 }, new Sharpy.List<int>() { 3, 4 }, new Sharpy.List<int>() { 5 }));
#line (10, 5) - (10, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 4, 5 }, result);
            }

            [Xunit.FactAttribute]
            public void TestChainEmptyIterablesReturnsEmpty()
            {
#line (14, 5) - (14, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (15, 5) - (15, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Chain(empty, empty));
#line (16, 5) - (16, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestChainSingleIterableBehavesLikeOriginal()
            {
#line (20, 5) - (20, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Chain(new Sharpy.List<int>() { 10, 20, 30 }));
#line (21, 5) - (21, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 10, 20, 30 }, result);
            }

            [Xunit.FactAttribute]
            public void TestChainWithEmptyIntermediateIterableSkipsIt()
            {
#line (25, 5) - (25, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (26, 5) - (26, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Chain(new Sharpy.List<int>() { 1 }, empty, new Sharpy.List<int>() { 2 }));
#line (27, 5) - (27, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, result);
            }

            [Xunit.FactAttribute]
            public void TestIsliceStopOnlyTakesFirstNElements()
            {
#line (33, 5) - (33, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> source = new Sharpy.List<int>()
                {
                    10,
                    20,
                    30,
                    40,
                    50
                };
#line (34, 5) - (34, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Islice(source, 3));
#line (35, 5) - (35, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (36, 5) - (36, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(10, result[0]);
#line (37, 5) - (37, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(20, result[1]);
#line (38, 5) - (38, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(30, result[2]);
            }

            [Xunit.FactAttribute]
            public void TestIsliceStartAndStopSkipsToStart()
            {
#line (42, 5) - (42, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> source = new Sharpy.List<int>()
                {
                    10,
                    20,
                    30,
                    40,
                    50
                };
#line (43, 5) - (43, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.IsliceRange(source, 1, 4));
#line (44, 5) - (44, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (45, 5) - (45, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(20, result[0]);
#line (46, 5) - (46, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(30, result[1]);
#line (47, 5) - (47, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(40, result[2]);
            }

            [Xunit.FactAttribute]
            public void TestIsliceWithStepSkipsElements()
            {
#line (51, 5) - (51, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> source = new Sharpy.List<int>()
                {
                    10,
                    20,
                    30,
                    40,
                    50,
                    60
                };
#line (52, 5) - (52, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.IsliceRange(source, 0, 6, 2));
#line (53, 5) - (53, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (54, 5) - (54, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(10, result[0]);
#line (55, 5) - (55, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(30, result[1]);
#line (56, 5) - (56, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(50, result[2]);
            }

            [Xunit.FactAttribute]
            public void TestIsliceNegativeStartReturnsEmpty()
            {
#line (60, 5) - (60, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> source = new Sharpy.List<int>()
                {
                    1
                };
#line (61, 5) - (61, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.IsliceRange(source, -1, 5));
#line (62, 5) - (62, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestIsliceZeroStepYieldsOnlyMatchingIndex()
            {
#line (66, 5) - (66, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> source = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (67, 5) - (67, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.IsliceRange(source, 0, 5, 0));
#line (68, 5) - (68, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (69, 5) - (69, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(1, result[0]);
            }

            [Xunit.FactAttribute]
            public void TestIsliceStartBeyondSourceReturnsEmpty()
            {
#line (73, 5) - (73, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> source = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (74, 5) - (74, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.IsliceRange(source, 10, 20));
#line (75, 5) - (75, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestCombinationsReturnsCorrectCombinations()
            {
#line (81, 5) - (81, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (82, 5) - (82, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<Sharpy.List<int>> results = new global::Sharpy.List<Sharpy.List<int>>(itertools.Combinations(items, 2));
#line (83, 5) - (83, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(results));
#line (84, 5) - (84, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, results[0]);
#line (85, 5) - (85, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3 }, results[1]);
#line (86, 5) - (86, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2, 3 }, results[2]);
            }

            [Xunit.FactAttribute]
            public void TestCombinationsRLargerThanPoolReturnsEmpty()
            {
#line (90, 5) - (90, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (91, 5) - (91, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<Sharpy.List<int>> results = new global::Sharpy.List<Sharpy.List<int>>(itertools.Combinations(items, 5));
#line (92, 5) - (92, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(results));
            }

            [Xunit.FactAttribute]
            public void TestCombinationsRZeroReturnsSingleEmptyList()
            {
#line (96, 5) - (96, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (97, 5) - (97, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<Sharpy.List<int>> results = new global::Sharpy.List<Sharpy.List<int>>(itertools.Combinations(items, 0));
#line (98, 5) - (98, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(results));
#line (99, 5) - (99, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(results[0]));
            }

            [Xunit.FactAttribute]
            public void TestCombinationsNegativeRThrowsValueError()
            {
#line (103, 5) - (103, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1
                };
#line (104, 5) - (107, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (105, 9) - (105, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                    new global::Sharpy.List<Sharpy.List<int>>(itertools.Combinations(items, -1));
                }));
            }

            [Xunit.FactAttribute]
            public void TestCombinationsREqualsPoolSizeReturnsSingleCombination()
            {
#line (109, 5) - (109, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (110, 5) - (110, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<Sharpy.List<int>> results = new global::Sharpy.List<Sharpy.List<int>>(itertools.Combinations(items, 3));
#line (111, 5) - (111, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(results));
#line (112, 5) - (112, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, results[0]);
            }

            [Xunit.FactAttribute]
            public void TestPermutationsDefaultRReturnsFullPermutations()
            {
#line (118, 5) - (118, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (119, 5) - (119, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<Sharpy.List<int>> results = new global::Sharpy.List<Sharpy.List<int>>(itertools.Permutations(items));
#line (120, 5) - (120, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(6, global::Sharpy.Builtins.Len(results));
#line (121, 5) - (121, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, results[0]);
#line (122, 5) - (122, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3, 2 }, results[1]);
            }

            [Xunit.FactAttribute]
            public void TestPermutationsWithRReturnsRLengthPermutations()
            {
#line (126, 5) - (126, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (127, 5) - (127, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<Sharpy.List<int>> results = new global::Sharpy.List<Sharpy.List<int>>(itertools.Permutations(items, 2));
#line (128, 5) - (128, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(6, global::Sharpy.Builtins.Len(results));
            }

            [Xunit.FactAttribute]
            public void TestPermutationsRLargerThanPoolReturnsEmpty()
            {
#line (132, 5) - (132, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (133, 5) - (133, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<Sharpy.List<int>> results = new global::Sharpy.List<Sharpy.List<int>>(itertools.Permutations(items, 5));
#line (134, 5) - (134, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(results));
            }

            [Xunit.FactAttribute]
            public void TestPermutationsNegativeRReturnsFullPermutations()
            {
#line (138, 5) - (138, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (139, 5) - (139, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<Sharpy.List<int>> results = new global::Sharpy.List<Sharpy.List<int>>(itertools.Permutations(items, -1));
#line (140, 5) - (140, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
            }

            [Xunit.FactAttribute]
            public void TestPermutationsSingleElementReturnsSinglePermutation()
            {
#line (144, 5) - (144, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    42
                };
#line (145, 5) - (145, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Sharpy.List<Sharpy.List<int>> results = new global::Sharpy.List<Sharpy.List<int>>(itertools.Permutations(items));
#line (146, 5) - (146, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(results));
#line (147, 5) - (147, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 42 }, results[0]);
            }
        }
    }
}
