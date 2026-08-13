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
using static Sharpy.Stdlib.Tests.Spy.Random.RandomTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Random
    {
        [global::Sharpy.SharpyModule("random.random_tests")]
        public static partial class RandomTests
        {
        }
    }

    public static partial class Random
    {
        public partial class RandomTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSeedProducesDeterministicResults()
            {
#line (30, 5) - (30, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(42);
#line (31, 5) - (31, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                double first = random.NextDouble();
#line (32, 5) - (32, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(42);
#line (33, 5) - (33, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                double second = random.NextDouble();
#line (34, 5) - (34, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Xunit.Assert.Equal(second, first);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNextDoubleReturnsValueInRange()
            {
#line (40, 5) - (40, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(123);
#line (41, 5) - (41, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                int i = 0;
#line (42, 5) - (50, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                while (i < 50)
#line hidden
                {
#line (43, 9) - (43, 45) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    double value = random.NextDouble();
#line (44, 9) - (44, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value >= 0.0d);
#line (45, 9) - (45, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value < 1.0d);
#line (46, 9) - (46, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestRandintReturnsValueInInclusiveRange110()
            {
#line (52, 5) - (52, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(99);
#line (53, 5) - (53, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                int i = 0;
#line (54, 5) - (60, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                while (i < 50)
#line hidden
                {
#line (55, 9) - (55, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    int value = random.Randint(1, 10);
#line (56, 9) - (56, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value >= 1);
#line (57, 9) - (57, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value <= 10);
#line (58, 9) - (58, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestRandintReturnsValueInInclusiveRange00()
            {
#line (62, 5) - (62, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(99);
#line (63, 5) - (63, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                int i = 0;
#line (64, 5) - (70, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                while (i < 50)
#line hidden
                {
#line (65, 9) - (65, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    int value = random.Randint(0, 0);
#line (66, 9) - (66, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value >= 0);
#line (67, 9) - (67, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value <= 0);
#line (68, 9) - (68, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestRandintReturnsValueInInclusiveRangeNeg55()
            {
#line (72, 5) - (72, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(99);
#line (73, 5) - (73, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                int i = 0;
#line (74, 5) - (82, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                while (i < 50)
#line hidden
                {
#line (75, 9) - (75, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    int value = random.Randint(-5, 5);
#line (76, 9) - (76, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value >= -5);
#line (77, 9) - (77, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value <= 5);
#line (78, 9) - (78, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestUniformReturnsValueInRange()
            {
#line (84, 5) - (84, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(77);
#line (85, 5) - (85, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                int i = 0;
#line (86, 5) - (94, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                while (i < 50)
#line hidden
                {
#line (87, 9) - (87, 49) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    double value = random.Uniform(1.0d, 5.0d);
#line (88, 9) - (88, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value >= 1.0d);
#line (89, 9) - (89, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.True(value <= 5.0d);
#line (90, 9) - (90, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestShuffleRearrangesElements()
            {
#line (96, 5) - (96, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(42);
#line (97, 5) - (97, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5,
                    6,
                    7,
                    8,
                    9,
                    10
                };
#line (98, 5) - (98, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Sharpy.List<int> original = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5,
                    6,
                    7,
                    8,
                    9,
                    10
                };
#line (99, 5) - (99, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Shuffle(lst);
#line (101, 5) - (101, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted<int>(original), global::Sharpy.Builtins.Sorted<int>(lst));
#line (103, 5) - (103, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Xunit.Assert.NotEqual(original, lst);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSampleReturnsKUniqueElements()
            {
#line (109, 5) - (109, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(42);
#line (110, 5) - (110, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Sharpy.List<int> population = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5,
                    6,
                    7,
                    8,
                    9,
                    10
                };
#line (111, 5) - (111, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Sharpy.List<int> sample = random.Sample(population, 3);
#line (112, 5) - (112, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(sample));
#line (113, 5) - (116, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                foreach (var __loopVar_0 in sample)
#line hidden
                {
                    var item = __loopVar_0;
#line (114, 9) - (114, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    Xunit.Assert.Contains(item, population);
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestSampleKLargerThanPopulationThrowsValueError()
            {
#line (118, 5) - (118, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Sharpy.List<int> population = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2
                };
#line (119, 5) - (122, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (120, 9) - (120, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    random.Sample(population, 5);
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
            public void TestSampleNegativeKThrowsValueError()
            {
#line (124, 5) - (124, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Sharpy.List<int> population = new Sharpy.List<int>()
#line hidden
                {
                    1
                };
#line (125, 5) - (128, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (126, 9) - (126, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                    random.Sample(population, -1);
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
            public void TestSampleZeroKReturnsEmptyList()
            {
#line (130, 5) - (130, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                random.Seed(42);
#line (131, 5) - (131, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Sharpy.List<int> population = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (132, 5) - (132, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Sharpy.List<int> sample = random.Sample(population, 0);
#line (133, 5) - (133, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/random/random_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(sample));
#line hidden
            }
        }
    }
}
#line default
