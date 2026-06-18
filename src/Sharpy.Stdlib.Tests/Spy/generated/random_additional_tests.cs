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
using random = global::Sharpy.RandomModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Random.RandomAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Random
    {
        [global::Sharpy.SharpyModule("random.random_additional_tests")]
        public static partial class RandomAdditionalTests
        {
        }
    }

    public static partial class Random
    {
        public partial class RandomAdditionalTestsTests
        {
            [Xunit.FactAttribute]
            public void TestRandrangeSingleArgReturnsInRange()
            {
#line (25, 5) - (25, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (26, 5) - (26, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (27, 5) - (33, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
                {
#line (28, 9) - (28, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Randrange(10);
#line (29, 9) - (29, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (30, 9) - (30, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val < 10);
#line (31, 9) - (31, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestRandrangeTwoArgsReturnsInRange()
            {
#line (35, 5) - (35, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (36, 5) - (36, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (37, 5) - (43, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
                {
#line (38, 9) - (38, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Randrange(5, 15);
#line (39, 9) - (39, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 5);
#line (40, 9) - (40, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val < 15);
#line (41, 9) - (41, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestRandrangeWithStepReturnsValidValues()
            {
#line (45, 5) - (45, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (46, 5) - (46, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (47, 5) - (54, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
                {
#line (48, 9) - (48, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Randrange(0, 10, 2);
#line (49, 9) - (49, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (50, 9) - (50, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val < 10);
#line (51, 9) - (51, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.Equal(0, val % 2);
#line (52, 9) - (52, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestRandrangeNegativeStep()
            {
#line (56, 5) - (56, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (57, 5) - (57, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (58, 5) - (65, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
                {
#line (59, 9) - (59, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Randrange(10, 0, -2);
#line (60, 9) - (60, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val > 0);
#line (61, 9) - (61, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val <= 10);
#line (62, 9) - (62, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.Equal(0, val % 2);
#line (63, 9) - (63, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestRandrangeEmptyRangeThrowsValueError()
            {
#line (67, 5) - (70, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (68, 9) - (68, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Randrange(5, 5);
                }));
            }

            [Xunit.FactAttribute]
            public void TestRandrangeZeroStepThrowsValueError()
            {
#line (72, 5) - (77, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (73, 9) - (73, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Randrange(0, 10, 0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestGaussMeanAndStdDevWithinTolerance()
            {
#line (79, 5) - (79, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (80, 5) - (80, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double mu = 5.0d;
#line (81, 5) - (81, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double sigma = 2.0d;
#line (82, 5) - (82, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int n = 10000;
#line (83, 5) - (83, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double total = 0.0d;
#line (84, 5) - (84, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double sumSq = 0.0d;
#line (85, 5) - (85, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (86, 5) - (91, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < n)
                {
#line (87, 9) - (87, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    double val = random.Gauss(mu, sigma);
#line (88, 9) - (88, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    total = total + val;
#line (89, 9) - (89, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    sumSq = sumSq + val * val;
#line (90, 9) - (90, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
                }

#line (91, 5) - (91, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double mean = total / n;
#line (92, 5) - (92, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double variance = (sumSq / n) - (mean * mean);
#line (93, 5) - (93, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double stddev = global::System.Math.Pow(variance, 0.5d);
#line (94, 5) - (94, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(mean - mu) < 0.1d);
#line (95, 5) - (95, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(stddev - sigma) < 0.1d);
            }

            [Xunit.FactAttribute]
            public void TestGaussZeroSigmaReturnsMu()
            {
#line (100, 5) - (100, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (101, 5) - (101, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (102, 5) - (108, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 10)
                {
#line (103, 9) - (103, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.Equal(3.0d, random.Gauss(3.0d, 0.0d));
#line (104, 9) - (104, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsReturnsValueInBitRange()
            {
#line (110, 5) - (110, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (111, 5) - (111, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (112, 5) - (118, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
                {
#line (113, 9) - (113, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Getrandbits(8);
#line (114, 9) - (114, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (115, 9) - (115, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val < 256);
#line (116, 9) - (116, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsZeroBitsReturnsZero()
            {
#line (120, 5) - (120, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Equal(0, random.Getrandbits(0));
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsOneBitReturnsZeroOrOne()
            {
#line (124, 5) - (124, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (125, 5) - (125, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (126, 5) - (132, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 50)
                {
#line (127, 9) - (127, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Getrandbits(1);
#line (128, 9) - (128, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (129, 9) - (129, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val <= 1);
#line (130, 9) - (130, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsNegativeBitsThrowsValueError()
            {
#line (134, 5) - (137, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (135, 9) - (135, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Getrandbits(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsTooManyBitsThrowsValueError()
            {
#line (139, 5) - (144, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (140, 9) - (140, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Getrandbits(31);
                }));
            }

            [Xunit.FactAttribute]
            public void TestChoicesUniformSelectionReturnsFromPopulation()
            {
#line (146, 5) - (146, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (147, 5) - (147, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<string> pop = new Sharpy.List<string>()
                {
                    "a",
                    "b",
                    "c"
                };
#line (148, 5) - (148, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<string> result = random.Choices(pop, k: 10);
#line (149, 5) - (149, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Equal(10, global::Sharpy.Builtins.Len(result));
#line (150, 5) - (153, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                foreach (var __loopVar_0 in result)
                {
                    var item = __loopVar_0;
#line (151, 9) - (151, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.Contains(item, pop);
                }
            }

            [Xunit.FactAttribute]
            public void TestChoicesEmptyPopulationThrowsIndexError()
            {
#line (155, 5) - (155, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<int> pop = new Sharpy.List<int>()
                {
                };
#line (156, 5) - (159, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (157, 9) - (157, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Choices(pop, k: 1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestChoicesKZeroReturnsEmpty()
            {
#line (161, 5) - (161, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (162, 5) - (162, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<int> pop = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (163, 5) - (163, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<int> result = random.Choices(pop, k: 0);
#line (164, 5) - (164, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }
        }
    }
}
