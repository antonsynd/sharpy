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
using math = global::Sharpy.MathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Itertools.ItertoolsAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Itertools
    {
        [global::Sharpy.SharpyModule("itertools.itertools_additional_tests")]
        public static partial class ItertoolsAdditionalTests
        {
        }
    }

    public static partial class Itertools
    {
        public partial class ItertoolsAdditionalTestsTests
        {
            [Xunit.FactAttribute]
            public void TestCountDefaultStartAndStep()
            {
#line (13, 5) - (13, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (14, 5) - (18, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                foreach (var __loopVar_0 in itertools.Count())
                {
                    var n = __loopVar_0;
#line (15, 9) - (15, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    result.Append(n);
#line (16, 9) - (18, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 5)
                    {
#line (17, 13) - (17, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                        break;
                    }
                }

#line (18, 5) - (18, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 2, 3, 4 }, result);
            }

            [Xunit.FactAttribute]
            public void TestCountCustomStartAndStep()
            {
#line (22, 5) - (22, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (23, 5) - (27, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                foreach (var __loopVar_1 in itertools.Count(10, 3))
                {
                    var n = __loopVar_1;
#line (24, 9) - (24, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    result.Append(n);
#line (25, 9) - (27, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 4)
                    {
#line (26, 13) - (26, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                        break;
                    }
                }

#line (27, 5) - (27, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 10, 13, 16, 19 }, result);
            }

            [Xunit.FactAttribute]
            public void TestCountNegativeStep()
            {
#line (31, 5) - (31, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (32, 5) - (36, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                foreach (var __loopVar_2 in itertools.Count(5, -1))
                {
                    var n = __loopVar_2;
#line (33, 9) - (33, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    result.Append(n);
#line (34, 9) - (36, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 4)
                    {
#line (35, 13) - (35, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                        break;
                    }
                }

#line (36, 5) - (36, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 5, 4, 3, 2 }, result);
            }

            [Xunit.FactAttribute]
            public void TestAccumulateRunningSum()
            {
#line (42, 5) - (42, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (43, 5) - (43, 88) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Accumulate(data, (int a, int b) => a + b));
#line (44, 5) - (44, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3, 6, 10, 15 }, result);
            }

            [Xunit.FactAttribute]
            public void TestAccumulateRunningProduct()
            {
#line (48, 5) - (48, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4
                };
#line (49, 5) - (49, 88) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Accumulate(data, (int a, int b) => a * b));
#line (50, 5) - (50, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 6, 24 }, result);
            }

            [Xunit.FactAttribute]
            public void TestAccumulateWithInitial()
            {
#line (54, 5) - (54, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (55, 5) - (55, 93) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Accumulate(data, (int a, int b) => a + b, 100));
#line (56, 5) - (56, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 100, 101, 103, 106 }, result);
            }

            [Xunit.FactAttribute]
            public void TestAccumulateEmptyIterable()
            {
#line (60, 5) - (60, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (61, 5) - (61, 89) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Accumulate(empty, (int a, int b) => a + b));
#line (62, 5) - (62, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestAccumulateSingleElement()
            {
#line (66, 5) - (66, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    42
                };
#line (67, 5) - (67, 88) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Accumulate(data, (int a, int b) => a + b));
#line (68, 5) - (68, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 42 }, result);
            }

            [Xunit.FactAttribute]
            public void TestDropwhileBasic()
            {
#line (74, 5) - (74, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    4,
                    6,
                    4,
                    1
                };
#line (75, 5) - (75, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Dropwhile((int x) => x < 5, data));
#line (76, 5) - (76, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 6, 4, 1 }, result);
            }

            [Xunit.FactAttribute]
            public void TestDropwhileNoneDropped()
            {
#line (80, 5) - (80, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (81, 5) - (81, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Dropwhile((int x) => x > 100, data));
#line (82, 5) - (82, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, result);
            }

            [Xunit.FactAttribute]
            public void TestDropwhileAllDropped()
            {
#line (86, 5) - (86, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (87, 5) - (87, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Dropwhile((int x) => x < 100, data));
#line (88, 5) - (88, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestTakewhileBasic()
            {
#line (94, 5) - (94, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    4,
                    6,
                    4,
                    1
                };
#line (95, 5) - (95, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Takewhile((int x) => x < 5, data));
#line (96, 5) - (96, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 4 }, result);
            }

            [Xunit.FactAttribute]
            public void TestTakewhileAllTaken()
            {
#line (100, 5) - (100, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (101, 5) - (101, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Takewhile((int x) => x < 100, data));
#line (102, 5) - (102, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, result);
            }

            [Xunit.FactAttribute]
            public void TestTakewhileNoneTaken()
            {
#line (106, 5) - (106, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (107, 5) - (107, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Takewhile((int x) => x > 100, data));
#line (108, 5) - (108, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestCompressBasic()
            {
#line (114, 5) - (114, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<string> data = new Sharpy.List<string>()
                {
                    "A",
                    "B",
                    "C",
                    "D",
                    "E",
                    "F"
                };
#line (115, 5) - (115, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<bool> sel = new Sharpy.List<bool>()
                {
                    true,
                    false,
                    true,
                    false,
                    true,
                    true
                };
#line (116, 5) - (116, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<string> result = new global::Sharpy.List<string>(itertools.Compress(data, sel));
#line (117, 5) - (117, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "A", "C", "E", "F" }, result);
            }

            [Xunit.FactAttribute]
            public void TestCompressShorterSelectors()
            {
#line (121, 5) - (121, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (122, 5) - (122, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<bool> sel = new Sharpy.List<bool>()
                {
                    true,
                    true
                };
#line (123, 5) - (123, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Compress(data, sel));
#line (124, 5) - (124, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, result);
            }

            [Xunit.FactAttribute]
            public void TestFilterfalseBasic()
            {
#line (130, 5) - (130, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4,
                    5,
                    6
                };
#line (131, 5) - (131, 86) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Filterfalse((int x) => x % 2 == 0, data));
#line (132, 5) - (132, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3, 5 }, result);
            }

            [Xunit.FactAttribute]
            public void TestFilterfalseNoneFiltered()
            {
#line (136, 5) - (136, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (137, 5) - (137, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Filterfalse((int x) => x > 100, data));
#line (138, 5) - (138, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, result);
            }

            [Xunit.FactAttribute]
            public void TestStarmapBasic()
            {
#line (144, 5) - (144, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> pairs = new Sharpy.List<global::System.ValueTuple<int, int>>()
                {
                    (2, 5),
                    (3, 2),
                    (10, 3)
                };
#line (145, 5) - (145, 97) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<double> result = new global::Sharpy.List<double>(itertools.Starmap((int a, int b) => math.Pow(a, b), pairs));
#line (146, 5) - (146, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<double>() { 32.0d, 9.0d, 1000.0d }, result);
            }

            [Xunit.FactAttribute]
            public void TestStarmapAddition()
            {
#line (150, 5) - (150, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> pairs = new Sharpy.List<global::System.ValueTuple<int, int>>()
                {
                    (1, 10),
                    (2, 20),
                    (3, 30)
                };
#line (151, 5) - (151, 86) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Starmap((int a, int b) => a + b, pairs));
#line (152, 5) - (152, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 11, 22, 33 }, result);
            }

            [Xunit.FactAttribute]
            public void TestZipLongestEvenLengths()
            {
#line (158, 5) - (158, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (159, 5) - (159, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
                {
                    4,
                    5,
                    6
                };
#line (160, 5) - (160, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.ZipLongest(a, b, 0));
#line (161, 5) - (161, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (162, 5) - (162, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((1, 4), result[0]);
#line (163, 5) - (163, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((2, 5), result[1]);
#line (164, 5) - (164, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((3, 6), result[2]);
            }

            [Xunit.FactAttribute]
            public void TestZipLongestUnevenLengths()
            {
#line (168, 5) - (168, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (169, 5) - (169, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
                {
                    4
                };
#line (170, 5) - (170, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.ZipLongest(a, b, -1));
#line (171, 5) - (171, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (172, 5) - (172, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((1, 4), result[0]);
#line (173, 5) - (173, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((2, -1), result[1]);
#line (174, 5) - (174, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((3, -1), result[2]);
            }

            [Xunit.FactAttribute]
            public void TestProductTwoIterables()
            {
#line (180, 5) - (180, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (181, 5) - (181, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
                {
                    3,
                    4
                };
#line (182, 5) - (182, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.Product(a, b));
#line (183, 5) - (183, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(result));
#line (184, 5) - (184, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((1, 3), result[0]);
#line (185, 5) - (185, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((1, 4), result[1]);
#line (186, 5) - (186, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((2, 3), result[2]);
#line (187, 5) - (187, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((2, 4), result[3]);
            }

            [Xunit.FactAttribute]
            public void TestProductWithRepeat()
            {
#line (191, 5) - (191, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    0,
                    1
                };
#line (192, 5) - (192, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
                {
                    0,
                    1
                };
#line (193, 5) - (193, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.Product(a, b));
#line (194, 5) - (194, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(result));
#line (195, 5) - (195, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((0, 0), result[0]);
#line (196, 5) - (196, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((0, 1), result[1]);
#line (197, 5) - (197, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((1, 0), result[2]);
#line (198, 5) - (198, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((1, 1), result[3]);
            }

            [Xunit.FactAttribute]
            public void TestProductEmptyIterable()
            {
#line (202, 5) - (202, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (203, 5) - (203, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (204, 5) - (204, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.Product(a, empty));
#line (205, 5) - (205, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestGroupbyWithKeyFunc()
            {
#line (211, 5) - (211, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<string> data = new Sharpy.List<string>()
                {
                    "aa",
                    "ab",
                    "ba",
                    "bb",
                    "bc"
                };
#line (212, 5) - (212, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, Sharpy.List<string>>> groups = new Sharpy.List<global::System.ValueTuple<string, Sharpy.List<string>>>()
                {
                };
#line (213, 5) - (218, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                foreach (var (key, group) in itertools.Groupby(data, (string s) => global::Sharpy.Slice.GetSlice(s, 0, 1, null)))
                {
#line (214, 9) - (214, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    Sharpy.List<string> items = new Sharpy.List<string>()
                    {
                    };
#line (215, 9) - (217, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    foreach (var __loopVar_3 in group)
                    {
                        var item = __loopVar_3;
#line (216, 13) - (216, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                        items.Append(item);
                    }

#line (217, 9) - (217, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    groups.Append((key, items));
                }

#line (218, 5) - (218, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(groups));
#line (219, 5) - (219, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal("a", groups[0].Item1);
#line (220, 5) - (220, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "aa", "ab" }, groups[0].Item2);
#line (221, 5) - (221, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal("b", groups[1].Item1);
#line (222, 5) - (222, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "ba", "bb", "bc" }, groups[1].Item2);
            }

            [Xunit.FactAttribute]
            public void TestGroupbyConsecutiveIdentical()
            {
#line (226, 5) - (226, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    1,
                    2,
                    2,
                    2,
                    3
                };
#line (227, 5) - (227, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> groups = new Sharpy.List<global::System.ValueTuple<int, int>>()
                {
                };
#line (228, 5) - (233, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                foreach (var (key, group) in itertools.Groupby(data, (int x) => x))
                {
#line (229, 9) - (229, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    int count = 0;
#line (230, 9) - (232, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    foreach (var __loopVar_4 in group)
                    {
                        var unused = __loopVar_4;
#line (231, 13) - (231, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                        count = count + 1;
                    }

#line (232, 9) - (232, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    groups.Append((key, count));
                }

#line (233, 5) - (233, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(groups));
#line (234, 5) - (234, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((1, 2), groups[0]);
#line (235, 5) - (235, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((2, 3), groups[1]);
#line (236, 5) - (236, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((3, 1), groups[2]);
            }

            [Xunit.FactAttribute]
            public void TestCombinationsWithReplacementBasic()
            {
#line (242, 5) - (242, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (243, 5) - (243, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.CombinationsWithReplacement(items, 2));
#line (244, 5) - (244, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(6, global::Sharpy.Builtins.Len(result));
#line (245, 5) - (245, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 1 }, result[0]);
#line (246, 5) - (246, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, result[1]);
#line (247, 5) - (247, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3 }, result[2]);
#line (248, 5) - (248, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2, 2 }, result[3]);
#line (249, 5) - (249, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2, 3 }, result[4]);
#line (250, 5) - (250, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 3 }, result[5]);
            }

            [Xunit.FactAttribute]
            public void TestCombinationsWithReplacementRZero()
            {
#line (254, 5) - (254, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (255, 5) - (255, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<Sharpy.List<int>> result = new global::Sharpy.List<Sharpy.List<int>>(itertools.CombinationsWithReplacement(items, 0));
#line (256, 5) - (256, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (257, 5) - (257, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result[0]));
            }

            [Xunit.FactAttribute]
            public void TestCombinationsWithReplacementNegativeRThrows()
            {
#line (261, 5) - (261, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1
                };
#line (262, 5) - (267, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (263, 9) - (263, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                    new global::Sharpy.List<Sharpy.List<int>>(itertools.CombinationsWithReplacement(items, -1));
                }));
            }

            [Xunit.FactAttribute]
            public void TestPairwiseBasic()
            {
#line (269, 5) - (269, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (270, 5) - (270, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.Pairwise(data));
#line (271, 5) - (271, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(result));
#line (272, 5) - (272, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((1, 2), result[0]);
#line (273, 5) - (273, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((2, 3), result[1]);
#line (274, 5) - (274, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((3, 4), result[2]);
#line (275, 5) - (275, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal((4, 5), result[3]);
            }

            [Xunit.FactAttribute]
            public void TestPairwiseSingleElement()
            {
#line (279, 5) - (279, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1
                };
#line (280, 5) - (280, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.Pairwise(data));
#line (281, 5) - (281, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestPairwiseEmpty()
            {
#line (285, 5) - (285, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (286, 5) - (286, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<int, int>> result = new global::Sharpy.List<global::System.ValueTuple<int, int>>(itertools.Pairwise(empty));
#line (287, 5) - (287, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestPairwiseStrings()
            {
#line (291, 5) - (291, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<string> data = new Sharpy.List<string>()
                {
                    "A",
                    "B",
                    "C",
                    "D"
                };
#line (292, 5) - (292, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, string>> result = new global::Sharpy.List<global::System.ValueTuple<string, string>>(itertools.Pairwise(data));
#line (293, 5) - (293, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (294, 5) - (294, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(("A", "B"), result[0]);
#line (295, 5) - (295, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(("B", "C"), result[1]);
#line (296, 5) - (296, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_additional_tests.spy"
                Xunit.Assert.Equal(("C", "D"), result[2]);
            }
        }
    }
}
