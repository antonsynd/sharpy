// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using bisect = global::Sharpy.BisectModule;
using random = global::Sharpy.RandomModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Cpython.CpythonBisectTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Cpython
    {
        [global::Sharpy.SharpyModule("cpython.cpython_bisect_tests")]
        public static partial class CpythonBisectTests
        {
        }
    }

    public static partial class Cpython
    {
        public partial class CpythonBisectTestsTests
        {
            [Xunit.FactAttribute]
            public void TestPrecomputedBisectRight()
            {
#line (28, 5) - (28, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (29, 5) - (29, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectRight(empty, 1));
#line (30, 5) - (30, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectRight(new Sharpy.List<int>() { 1 }, 0));
#line (31, 5) - (31, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectRight(new Sharpy.List<int>() { 1 }, 1));
#line (32, 5) - (32, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectRight(new Sharpy.List<int>() { 1 }, 2));
#line (33, 5) - (33, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectRight(new Sharpy.List<int>() { 1, 1 }, 0));
#line (34, 5) - (34, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectRight(new Sharpy.List<int>() { 1, 1 }, 1));
#line (35, 5) - (35, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectRight(new Sharpy.List<int>() { 1, 1 }, 2));
#line (36, 5) - (36, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectRight(new Sharpy.List<int>() { 1, 1, 1 }, 1));
#line (37, 5) - (37, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(4, bisect.BisectRight(new Sharpy.List<int>() { 1, 1, 1, 1 }, 1));
#line (38, 5) - (38, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectRight(new Sharpy.List<int>() { 1, 2 }, 0));
#line (39, 5) - (39, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectRight(new Sharpy.List<int>() { 1, 2 }, 1));
#line (40, 5) - (40, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectRight(new Sharpy.List<int>() { 1, 2 }, 2));
#line (41, 5) - (41, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectRight(new Sharpy.List<int>() { 1, 2 }, 3));
#line (42, 5) - (42, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectRight(new Sharpy.List<int>() { 1, 2, 3 }, 0));
#line (43, 5) - (43, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectRight(new Sharpy.List<int>() { 1, 2, 3 }, 1));
#line (44, 5) - (44, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectRight(new Sharpy.List<int>() { 1, 2, 3 }, 2));
#line (45, 5) - (45, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectRight(new Sharpy.List<int>() { 1, 2, 3 }, 3));
#line (46, 5) - (46, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectRight(new Sharpy.List<int>() { 1, 2, 3 }, 4));
#line (47, 5) - (47, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectRight(new Sharpy.List<int>() { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 }, 2));
#line (48, 5) - (48, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(6, bisect.BisectRight(new Sharpy.List<int>() { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 }, 3));
#line (49, 5) - (49, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(10, bisect.BisectRight(new Sharpy.List<int>() { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 }, 4));
#line (50, 5) - (50, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(10, bisect.BisectRight(new Sharpy.List<int>() { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 }, 5));
            }

            [Xunit.FactAttribute]
            public void TestPrecomputedBisectLeft()
            {
#line (54, 5) - (54, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (55, 5) - (55, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(empty, 1));
#line (56, 5) - (56, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(new Sharpy.List<int>() { 1 }, 0));
#line (57, 5) - (57, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(new Sharpy.List<int>() { 1 }, 1));
#line (58, 5) - (58, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectLeft(new Sharpy.List<int>() { 1 }, 2));
#line (59, 5) - (59, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(new Sharpy.List<int>() { 1, 1 }, 0));
#line (60, 5) - (60, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(new Sharpy.List<int>() { 1, 1 }, 1));
#line (61, 5) - (61, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectLeft(new Sharpy.List<int>() { 1, 1 }, 2));
#line (62, 5) - (62, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(new Sharpy.List<int>() { 1, 1, 1 }, 1));
#line (63, 5) - (63, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(4, bisect.BisectLeft(new Sharpy.List<int>() { 1, 1, 1, 1 }, 2));
#line (64, 5) - (64, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2 }, 0));
#line (65, 5) - (65, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2 }, 1));
#line (66, 5) - (66, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2 }, 2));
#line (67, 5) - (67, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2 }, 3));
#line (68, 5) - (68, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2, 3 }, 1));
#line (69, 5) - (69, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2, 3 }, 2));
#line (70, 5) - (70, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2, 3 }, 3));
#line (71, 5) - (71, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2, 3 }, 4));
#line (72, 5) - (72, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 }, 2));
#line (73, 5) - (73, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 }, 3));
#line (74, 5) - (74, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(6, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 }, 4));
#line (75, 5) - (75, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(10, bisect.BisectLeft(new Sharpy.List<int>() { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 }, 5));
            }

            [Xunit.FactAttribute]
            public void TestPrecomputedFloatElements()
            {
#line (80, 5) - (80, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectRight(new Sharpy.List<double>() { 1.0d, 2.0d }, 1.5d));
#line (81, 5) - (81, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectLeft(new Sharpy.List<double>() { 1.0d, 2.0d }, 1.5d));
#line (82, 5) - (82, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectRight(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d }, 1.5d));
#line (83, 5) - (83, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectLeft(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d }, 2.5d));
            }

            [Xunit.FactAttribute]
            public void TestRandom()
            {
#line (89, 5) - (89, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                random.Seed(17);
#line (90, 5) - (90, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                int n = 25;
#line (91, 5) - (91, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                int i = 0;
#line (92, 5) - (114, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                while (i < n)
                {
#line (93, 9) - (93, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    Sharpy.List<int> data = new Sharpy.List<int>()
                    {
                    };
#line (94, 9) - (94, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    int j = 0;
#line (95, 9) - (98, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    while (j < i)
                    {
#line (96, 13) - (96, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                        data.Append(random.Randrange(0, n, 2));
#line (97, 13) - (97, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                        j = j + 1;
                    }

#line (98, 9) - (98, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    data.Sort();
#line (99, 9) - (99, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    int elem = random.Randrange(-1, n + 1);
#line (100, 9) - (100, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    int ip = bisect.BisectLeft(data, elem);
#line (101, 9) - (103, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    if (ip < global::Sharpy.Builtins.Len(data))
                    {
#line (102, 13) - (102, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                        Xunit.Assert.True(elem <= data[ip]);
                    }

#line (103, 9) - (105, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    if (ip > 0)
                    {
#line (104, 13) - (104, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                        Xunit.Assert.True(data[ip - 1] < elem);
                    }

#line (105, 9) - (105, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    ip = bisect.BisectRight(data, elem);
#line (106, 9) - (108, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    if (ip < global::Sharpy.Builtins.Len(data))
                    {
#line (107, 13) - (107, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                        Xunit.Assert.True(elem < data[ip]);
                    }

#line (108, 9) - (110, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    if (ip > 0)
                    {
#line (109, 13) - (109, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                        Xunit.Assert.True(data[ip - 1] <= elem);
                    }

#line (110, 9) - (110, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestKeywordArgs()
            {
#line (116, 5) - (116, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    10,
                    20,
                    30,
                    40,
                    50
                };
#line (117, 5) - (117, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectLeft(a: data, x: 25, lo: 1, hi: 3));
#line (118, 5) - (118, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectRight(a: data, x: 25, lo: 1, hi: 3));
#line (119, 5) - (119, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.Bisect(a: data, x: 25, lo: 1, hi: 3));
#line (120, 5) - (120, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                bisect.InsortLeft(a: data, x: 25, lo: 1, hi: 3);
#line (121, 5) - (121, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                bisect.InsortRight(a: data, x: 25, lo: 1, hi: 3);
#line (122, 5) - (122, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                bisect.Insort(a: data, x: 25, lo: 1, hi: 3);
#line (123, 5) - (123, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 10, 20, 25, 25, 25, 30, 40, 50 }, data);
            }

            [Xunit.FactAttribute]
            public void TestBisectBackcompatibility()
            {
#line (129, 5) - (129, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    1,
                    2,
                    2,
                    3,
                    3,
                    3,
                    4
                };
#line (130, 5) - (130, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                int i = 0;
#line (131, 5) - (135, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                while (i < 6)
                {
#line (132, 9) - (132, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    Xunit.Assert.Equal(bisect.BisectRight(a, i), bisect.Bisect(a, i));
#line (133, 9) - (133, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestInsortBackcompatibility()
            {
#line (137, 5) - (137, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<int> a1 = new Sharpy.List<int>()
                {
                    1,
                    3,
                    5
                };
#line (138, 5) - (138, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<int> a2 = new Sharpy.List<int>()
                {
                    1,
                    3,
                    5
                };
#line (139, 5) - (139, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                bisect.Insort(a1, 4);
#line (140, 5) - (140, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                bisect.InsortRight(a2, 4);
#line (141, 5) - (141, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(a2, a1);
            }

            [Xunit.FactAttribute]
            public void TestVsBuiltinSort()
            {
#line (147, 5) - (147, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                random.Seed(23);
#line (148, 5) - (148, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<string> digits = new Sharpy.List<string>()
                {
                    "0",
                    "1",
                    "2",
                    "3",
                    "4",
                    "5",
                    "6",
                    "7",
                    "8",
                    "9"
                };
#line (149, 5) - (149, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<string> insorted = new Sharpy.List<string>()
                {
                };
#line (150, 5) - (150, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                int i = 0;
#line (151, 5) - (158, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                while (i < 200)
                {
#line (152, 9) - (152, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    int d = random.Randint(0, 9);
#line (153, 9) - (157, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    if (d % 2 == 0)
                    {
#line (154, 13) - (154, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                        bisect.InsortLeft(insorted, digits[d]);
                    }
                    else
                    {
#line (156, 13) - (156, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                        bisect.InsortRight(insorted, digits[d]);
                    }

#line (157, 9) - (157, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    i = i + 1;
                }

#line (158, 5) - (158, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(insorted, global::Sharpy.Builtins.Sorted<string>(insorted));
            }

            [Xunit.FactAttribute]
            public void TestDocExampleGrades()
            {
#line (164, 5) - (164, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<int> breakpoints = new Sharpy.List<int>()
                {
                    60,
                    70,
                    80,
                    90
                };
#line (165, 5) - (165, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                string grades = "FDCBA";
#line (166, 5) - (166, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<int> scores = new Sharpy.List<int>()
                {
                    33,
                    99,
                    77,
                    70,
                    89,
                    90,
                    100
                };
#line (167, 5) - (167, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<string> result = new Sharpy.List<string>()
                {
                };
#line (168, 5) - (171, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                foreach (var __loopVar_0 in scores)
                {
                    var score = __loopVar_0;
#line (169, 9) - (169, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    int idx = bisect.Bisect(breakpoints, score);
#line (170, 9) - (170, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                    result.Append(global::Sharpy.StringHelpers.GetItem(grades, idx));
                }

#line (171, 5) - (171, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "F", "A", "C", "C", "B", "A", "A" }, result);
            }

            [Xunit.FactAttribute]
            public void TestDocExampleColors()
            {
#line (175, 5) - (175, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> data = new Sharpy.List<global::System.ValueTuple<string, int>>()
                {
                    ("red", 5),
                    ("blue", 1),
                    ("yellow", 8),
                    ("black", 0)
                };
#line (176, 5) - (176, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                data.Sort(key: r => r.Item2);
#line (177, 5) - (177, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Sharpy.List<int> keys = new Sharpy.List<int>(data.Select((global::System.ValueTuple<string, int> r) => r.Item2));
#line (178, 5) - (178, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(("black", 0), data[bisect.BisectLeft(keys, 0)]);
#line (179, 5) - (179, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(("blue", 1), data[bisect.BisectLeft(keys, 1)]);
#line (180, 5) - (180, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(("red", 5), data[bisect.BisectLeft(keys, 5)]);
#line (181, 5) - (181, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy"
                Xunit.Assert.Equal(("yellow", 8), data[bisect.BisectLeft(keys, 8)]);
            }
        }
    }
}
