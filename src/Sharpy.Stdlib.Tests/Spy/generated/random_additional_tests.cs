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
#line (25, 5) - (25, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (26, 5) - (26, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (27, 5) - (33, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
#line hidden
                {
#line (28, 9) - (28, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Randrange(10);
#line (29, 9) - (29, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (30, 9) - (30, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val < 10);
#line (31, 9) - (31, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestRandrangeTwoArgsReturnsInRange()
            {
#line (35, 5) - (35, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (36, 5) - (36, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (37, 5) - (43, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
#line hidden
                {
#line (38, 9) - (38, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Randrange(5, 15);
#line (39, 9) - (39, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 5);
#line (40, 9) - (40, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val < 15);
#line (41, 9) - (41, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestRandrangeWithStepReturnsValidValues()
            {
#line (45, 5) - (45, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (46, 5) - (46, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (47, 5) - (54, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
#line hidden
                {
#line (48, 9) - (48, 47) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Randrange(0, 10, 2);
#line (49, 9) - (49, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (50, 9) - (50, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val < 10);
#line (51, 9) - (51, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.Equal(0, global::Sharpy.Builtins.FloorMod(val, 2));
#line (52, 9) - (52, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestRandrangeNegativeStep()
            {
#line (56, 5) - (56, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (57, 5) - (57, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (58, 5) - (65, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
#line hidden
                {
#line (59, 9) - (59, 48) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Randrange(10, 0, -2);
#line (60, 9) - (60, 24) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val > 0);
#line (61, 9) - (61, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val <= 10);
#line (62, 9) - (62, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.Equal(0, global::Sharpy.Builtins.FloorMod(val, 2));
#line (63, 9) - (63, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestRandrangeEmptyRangeThrowsValueError()
            {
#line (67, 5) - (70, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (68, 9) - (68, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Randrange(5, 5);
#line hidden
                }
                catch (ValueError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestRandrangeZeroStepThrowsValueError()
            {
#line (72, 5) - (77, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (73, 9) - (73, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Randrange(0, 10, 0);
#line hidden
                }
                catch (ValueError)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGaussMeanAndStdDevWithinTolerance()
            {
#line (79, 5) - (79, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (80, 5) - (80, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double mu = 5.0d;
#line (81, 5) - (81, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double sigma = 2.0d;
#line (82, 5) - (82, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int n = 10000;
#line (83, 5) - (83, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double total = 0.0d;
#line (84, 5) - (84, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double sumSq = 0.0d;
#line (85, 5) - (85, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (86, 5) - (91, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < n)
#line hidden
                {
#line (87, 9) - (87, 46) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    double val = random.Gauss(mu, sigma);
#line (88, 9) - (88, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    total = total + val;
#line (89, 9) - (89, 36) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    sumSq = sumSq + val * val;
#line (90, 9) - (90, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
#line hidden
                }

#line (91, 5) - (91, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double mean = total / n;
#line (92, 5) - (92, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double variance = (sumSq / n) - (mean * mean);
#line (93, 5) - (93, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                double stddev = global::System.Math.Pow(variance, 0.5d);
#line (94, 5) - (94, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(mean - mu) < 0.1d);
#line (95, 5) - (95, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(stddev - sigma) < 0.1d);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGaussZeroSigmaReturnsMu()
            {
#line (100, 5) - (100, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (101, 5) - (101, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (102, 5) - (108, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 10)
#line hidden
                {
#line (103, 9) - (103, 46) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.Equal(3.0d, random.Gauss(3.0d, 0.0d));
#line (104, 9) - (104, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsReturnsValueInBitRange()
            {
#line (110, 5) - (110, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (111, 5) - (111, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (112, 5) - (118, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 100)
#line hidden
                {
#line (113, 9) - (113, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Getrandbits(8);
#line (114, 9) - (114, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (115, 9) - (115, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val < 256);
#line (116, 9) - (116, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsZeroBitsReturnsZero()
            {
#line (120, 5) - (120, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Equal(0, random.Getrandbits(0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsOneBitReturnsZeroOrOne()
            {
#line (124, 5) - (124, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (125, 5) - (125, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                int i = 0;
#line (126, 5) - (132, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                while (i < 50)
#line hidden
                {
#line (127, 9) - (127, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    int val = random.Getrandbits(1);
#line (128, 9) - (128, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (129, 9) - (129, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.True(val <= 1);
#line (130, 9) - (130, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsNegativeBitsThrowsValueError()
            {
#line (134, 5) - (137, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (135, 9) - (135, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Getrandbits(-1);
#line hidden
                }
                catch (ValueError)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsTooManyBitsThrowsValueError()
            {
#line (139, 5) - (144, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (140, 9) - (140, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Getrandbits(31);
#line hidden
                }
                catch (ValueError)
                {
                    __raised_3 = true;
                }

                if (!__raised_3)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestChoicesUniformSelectionReturnsFromPopulation()
            {
#line (146, 5) - (146, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (147, 5) - (147, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<string> pop = new Sharpy.List<string>()
#line hidden
                {
                    "a",
                    "b",
                    "c"
                };
#line (148, 5) - (148, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<string> result = random.Choices(pop, k: 10);
#line (149, 5) - (149, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Equal(10, global::Sharpy.Builtins.Len(result));
#line (150, 5) - (153, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                foreach (var __loopVar_4 in result)
#line hidden
                {
                    var item = __loopVar_4;
#line (151, 9) - (151, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    Xunit.Assert.Contains(item, pop);
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestChoicesEmptyPopulationThrowsIndexError()
            {
#line (155, 5) - (155, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<int> pop = new Sharpy.List<int>()
#line hidden
                {
                };
#line (156, 5) - (159, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                bool __raised_5 = false;
#line hidden
                try
                {
#line (157, 9) - (157, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                    random.Choices(pop, k: 1);
#line hidden
                }
                catch (IndexError)
                {
                    __raised_5 = true;
                }

                if (!__raised_5)
                    throw new global::Sharpy.AssertionError("Expected IndexError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestChoicesKZeroReturnsEmpty()
            {
#line (161, 5) - (161, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                random.Seed(42);
#line (162, 5) - (162, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<int> pop = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (163, 5) - (163, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Sharpy.List<int> result = random.Choices(pop, k: 0);
#line (164, 5) - (164, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }
        }
    }
}
#line default
