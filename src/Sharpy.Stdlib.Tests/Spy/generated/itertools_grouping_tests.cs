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
using static Sharpy.Stdlib.Tests.Spy.Itertools.ItertoolsGroupingTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Itertools
    {
        [global::Sharpy.SharpyModule("itertools.itertools_grouping_tests")]
        public static partial class ItertoolsGroupingTests
        {
        }
    }

    public static partial class Itertools
    {
        public partial class ItertoolsGroupingTestsTests
        {
            [Xunit.FactAttribute]
            public void TestGroupbyEmptyIterableReturnsEmpty()
            {
#line (11, 5) - (11, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (12, 5) - (12, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, Sharpy.List<int>>> groups = new Sharpy.List<global::System.ValueTuple<int, Sharpy.List<int>>>()
#line hidden
                {
                };
#line (13, 5) - (18, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                foreach (var (key, group) in itertools.Groupby(empty, (int x) => x))
#line hidden
                {
#line (14, 9) - (14, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    Sharpy.List<int> items = new Sharpy.List<int>()
#line hidden
                    {
                    };
#line (15, 9) - (17, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    foreach (var __loopVar_0 in group)
#line hidden
                    {
                        var item = __loopVar_0;
#line (16, 13) - (16, 31) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                        items.Append(item);
#line hidden
                    }

#line (17, 9) - (17, 36) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    groups.Append((key, items));
#line hidden
                }

#line (18, 5) - (18, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(groups));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGroupbyNonConsecutiveSameKeysCreatesSeparateGroups()
            {
#line (22, 5) - (22, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    1,
                    2
                };
#line (23, 5) - (23, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, Sharpy.List<int>>> groups = new Sharpy.List<global::System.ValueTuple<int, Sharpy.List<int>>>()
#line hidden
                {
                };
#line (24, 5) - (29, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                foreach (var (key, group) in itertools.Groupby(data, (int x) => x))
#line hidden
                {
#line (25, 9) - (25, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    Sharpy.List<int> items = new Sharpy.List<int>()
#line hidden
                    {
                    };
#line (26, 9) - (28, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    foreach (var __loopVar_1 in group)
#line hidden
                    {
                        var item = __loopVar_1;
#line (27, 13) - (27, 31) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                        items.Append(item);
#line hidden
                    }

#line (28, 9) - (28, 36) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    groups.Append((key, items));
#line hidden
                }

#line (29, 5) - (29, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(groups));
#line (30, 5) - (30, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(1, groups.GetItemUnchecked(0).Item1);
#line (31, 5) - (31, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(2, groups.GetItemUnchecked(1).Item1);
#line (32, 5) - (32, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(1, groups.GetItemUnchecked(2).Item1);
#line (33, 5) - (33, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(2, groups.GetItemUnchecked(3).Item1);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGroupbyByStringLengthGroupsStringsCorrectly()
            {
#line (37, 5) - (37, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<string> data = new Sharpy.List<string>()
#line hidden
                {
                    "a",
                    "ab",
                    "b",
                    "bc"
                };
#line (38, 5) - (38, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, Sharpy.List<string>>> groups = new Sharpy.List<global::System.ValueTuple<int, Sharpy.List<string>>>()
#line hidden
                {
                };
#line (39, 5) - (44, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                foreach (var (key, group) in itertools.Groupby(data, (string s) => s.Length))
#line hidden
                {
#line (40, 9) - (40, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    Sharpy.List<string> items = new Sharpy.List<string>()
#line hidden
                    {
                    };
#line (41, 9) - (43, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    foreach (var __loopVar_2 in group)
#line hidden
                    {
                        var item = __loopVar_2;
#line (42, 13) - (42, 31) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                        items.Append(item);
#line hidden
                    }

#line (43, 9) - (43, 36) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    groups.Append((key, items));
#line hidden
                }

#line (44, 5) - (44, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(groups));
#line (45, 5) - (45, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(1, groups.GetItemUnchecked(0).Item1);
#line (46, 5) - (46, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a" }, groups.GetItemUnchecked(0).Item2);
#line (47, 5) - (47, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(2, groups.GetItemUnchecked(1).Item1);
#line (48, 5) - (48, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "ab" }, groups.GetItemUnchecked(1).Item2);
#line (49, 5) - (49, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(1, groups.GetItemUnchecked(2).Item1);
#line (50, 5) - (50, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "b" }, groups.GetItemUnchecked(2).Item2);
#line (51, 5) - (51, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(2, groups.GetItemUnchecked(3).Item1);
#line (52, 5) - (52, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "bc" }, groups.GetItemUnchecked(3).Item2);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGroupbyAllSameKeyReturnsSingleGroup()
            {
#line (56, 5) - (56, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    5,
                    5,
                    5
                };
#line (57, 5) - (57, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, Sharpy.List<int>>> groups = new Sharpy.List<global::System.ValueTuple<int, Sharpy.List<int>>>()
#line hidden
                {
                };
#line (58, 5) - (63, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                foreach (var (key, group) in itertools.Groupby(data, (int x) => x))
#line hidden
                {
#line (59, 9) - (59, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    Sharpy.List<int> items = new Sharpy.List<int>()
#line hidden
                    {
                    };
#line (60, 9) - (62, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    foreach (var __loopVar_3 in group)
#line hidden
                    {
                        var item = __loopVar_3;
#line (61, 13) - (61, 31) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                        items.Append(item);
#line hidden
                    }

#line (62, 9) - (62, 36) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                    groups.Append((key, items));
#line hidden
                }

#line (63, 5) - (63, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(groups));
#line (64, 5) - (64, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(5, groups.GetItemUnchecked(0).Item1);
#line (65, 5) - (65, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 5, 5, 5 }, groups.GetItemUnchecked(0).Item2);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPairwiseTwoElementsReturnsSinglePair()
            {
#line (71, 5) - (71, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    10,
                    20
                };
#line (72, 5) - (72, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.Pairwise(data));
#line (73, 5) - (73, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (74, 5) - (74, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal((10, 20), result.GetItemUnchecked(0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStarmapEmptyIterableReturnsEmpty()
            {
#line (80, 5) - (80, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> pairs = new Sharpy.List<global::System.ValueTuple<int, int>>()
#line hidden
                {
                };
#line (81, 5) - (81, 86) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Starmap((int a, int b) => a + b, pairs));
#line (82, 5) - (82, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStarmapSinglePairReturnsOneResult()
            {
#line (86, 5) - (86, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> pairs = new Sharpy.List<global::System.ValueTuple<int, int>>()
#line hidden
                {
                    (3, 7)
                };
#line (87, 5) - (87, 86) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Starmap((int a, int b) => a * b, pairs));
#line (88, 5) - (88, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 21 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestZipLongestEmptyAndNonEmptyFillsWithDefault()
            {
#line (94, 5) - (94, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (95, 5) - (95, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2
                };
#line (96, 5) - (96, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.ZipLongest(empty, b, 0));
#line (97, 5) - (97, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line (98, 5) - (98, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal((0, 1), result.GetItemUnchecked(0));
#line (99, 5) - (99, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal((0, 2), result.GetItemUnchecked(1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestZipLongestBothEmptyReturnsEmpty()
            {
#line (103, 5) - (103, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                };
#line (104, 5) - (104, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                };
#line (105, 5) - (105, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.ZipLongest(a, b, 0));
#line (106, 5) - (106, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestZipLongestWithFillvalueUsesCustomFillvalue()
            {
#line (110, 5) - (110, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (111, 5) - (111, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                    4,
                    5
                };
#line (112, 5) - (112, 75) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.ZipLongest(a, b, 99));
#line (113, 5) - (113, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (114, 5) - (114, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal((1, 4), result.GetItemUnchecked(0));
#line (115, 5) - (115, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal((2, 5), result.GetItemUnchecked(1));
#line (116, 5) - (116, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal((3, 99), result.GetItemUnchecked(2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainMultipleIterablesConcatenatesAll()
            {
#line (122, 5) - (122, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2
                };
#line (123, 5) - (123, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    4
                };
#line (124, 5) - (124, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> c = new Sharpy.List<int>()
#line hidden
                {
                    5
                };
#line (125, 5) - (125, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Chain(a, b, c));
#line (126, 5) - (126, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 4, 5 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainWithEmptyIterableSkipsEmpty()
            {
#line (130, 5) - (130, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2
                };
#line (131, 5) - (131, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (132, 5) - (132, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Chain(a, empty));
#line (133, 5) - (133, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainAllEmptyReturnsEmpty()
            {
#line (137, 5) - (137, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (138, 5) - (138, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Chain(empty, empty));
#line (139, 5) - (139, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainSingleIterableReturnsAllElements()
            {
#line (143, 5) - (143, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    42
                };
#line (144, 5) - (144, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Chain(a));
#line (145, 5) - (145, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 42 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestAccumulateDefaultSumFunctionReturnsCumulativeSums()
            {
#line (151, 5) - (151, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4
                };
#line (152, 5) - (152, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Accumulate(data));
#line (153, 5) - (153, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_grouping_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3, 6, 10 }, result);
#line hidden
            }
        }
    }
}
#line default
