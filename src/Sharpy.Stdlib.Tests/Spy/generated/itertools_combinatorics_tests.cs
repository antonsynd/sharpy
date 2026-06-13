// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using itertools = global::Sharpy.Itertools;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Itertools.ItertoolsCombinatoricsTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Itertools
    {
        [global::Sharpy.SharpyModule("itertools.itertools_combinatorics_tests")]
        public static partial class ItertoolsCombinatoricsTests
        {
        }
    }

    public static partial class Itertools
    {
        public partial class ItertoolsCombinatoricsTestsTests
        {
            [Xunit.FactAttribute]
            public void TestCombinationsEmptyPoolRZeroReturnsSingleEmptyTuple()
            {
#line (10, 5) - (10, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (11, 5) - (11, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.Combinations(empty, 0));
#line (12, 5) - (12, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (13, 5) - (13, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result[0]));
            }

            [Xunit.FactAttribute]
            public void TestCombinationsFourChooseTwoReturns6Combinations()
            {
#line (17, 5) - (17, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4
                };
#line (18, 5) - (18, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.Combinations(items, 2));
#line (19, 5) - (19, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(6, global::Sharpy.Builtins.Len(result));
#line (20, 5) - (20, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, result[0]);
#line (21, 5) - (21, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3 }, result[1]);
#line (22, 5) - (22, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 4 }, result[2]);
#line (23, 5) - (23, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2, 3 }, result[3]);
#line (24, 5) - (24, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2, 4 }, result[4]);
#line (25, 5) - (25, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 4 }, result[5]);
            }

            [Xunit.FactAttribute]
            public void TestCombinationsWithReplacementSingleElementR3ReturnsTriple()
            {
#line (31, 5) - (31, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1
                };
#line (32, 5) - (32, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.CombinationsWithReplacement(items, 3));
#line (33, 5) - (33, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (34, 5) - (34, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 1, 1 }, result[0]);
            }

            [Xunit.FactAttribute]
            public void TestCombinationsWithReplacementTwoChooseTwoReturns3Combinations()
            {
#line (38, 5) - (38, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (39, 5) - (39, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.CombinationsWithReplacement(items, 2));
#line (40, 5) - (40, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (41, 5) - (41, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 1 }, result[0]);
#line (42, 5) - (42, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, result[1]);
#line (43, 5) - (43, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2, 2 }, result[2]);
            }

            [Xunit.FactAttribute]
            public void TestCombinationsWithReplacementEmptyPoolRZeroReturnsSingleEmptyTuple()
            {
#line (47, 5) - (47, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (48, 5) - (48, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.CombinationsWithReplacement(empty, 0));
#line (49, 5) - (49, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (50, 5) - (50, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result[0]));
            }

            [Xunit.FactAttribute]
            public void TestPermutationsRZeroReturnsSingleEmptyTuple()
            {
#line (56, 5) - (56, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (57, 5) - (57, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.Permutations(items, 0));
#line (58, 5) - (58, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (59, 5) - (59, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result[0]));
            }

            [Xunit.FactAttribute]
            public void TestPermutationsFourChooseTwoReturns12Permutations()
            {
#line (63, 5) - (63, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4
                };
#line (64, 5) - (64, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.Permutations(items, 2));
#line (65, 5) - (65, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(12, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestPermutationsFullPermutationsVerifyFirstAndLast()
            {
#line (69, 5) - (69, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (70, 5) - (70, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.Permutations(items));
#line (71, 5) - (71, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(6, global::Sharpy.Builtins.Len(result));
#line (72, 5) - (72, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, result[0]);
#line (73, 5) - (73, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 2, 1 }, result[5]);
            }

            [Xunit.FactAttribute]
            public void TestProductTwoIterablesReturnsCartesianProduct()
            {
#line (79, 5) - (79, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (80, 5) - (80, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
                {
                    3,
                    4
                };
#line (81, 5) - (81, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.Product(a, b));
#line (82, 5) - (82, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(result));
#line (83, 5) - (83, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((1, 3), result[0]);
#line (84, 5) - (84, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((1, 4), result[1]);
#line (85, 5) - (85, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((2, 3), result[2]);
#line (86, 5) - (86, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((2, 4), result[3]);
            }

            [Xunit.FactAttribute]
            public void TestProductThreeIterablesReturnsCartesianProduct()
            {
#line (90, 5) - (90, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (91, 5) - (91, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
                {
                    3,
                    4
                };
#line (92, 5) - (92, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> c = new Sharpy.List<int>()
                {
                    5,
                    6
                };
#line (93, 5) - (93, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int, int>>(itertools.Product(a, b, c));
#line (94, 5) - (94, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(8, global::Sharpy.Builtins.Len(result));
#line (95, 5) - (95, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((1, 3, 5), result[0]);
#line (96, 5) - (96, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((1, 3, 6), result[1]);
#line (97, 5) - (97, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((2, 4, 6), result[7]);
            }

            [Xunit.FactAttribute]
            public void TestProductSamePoolWithSelfYieldsPairsWithRepetition()
            {
#line (101, 5) - (101, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (102, 5) - (102, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (103, 5) - (103, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.Product(a, b));
#line (104, 5) - (104, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(result));
#line (105, 5) - (105, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((1, 1), result[0]);
#line (106, 5) - (106, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((1, 2), result[1]);
#line (107, 5) - (107, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((2, 1), result[2]);
#line (108, 5) - (108, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_combinatorics_tests.spy"
                Xunit.Assert.Equal((2, 2), result[3]);
            }
        }
    }
}
