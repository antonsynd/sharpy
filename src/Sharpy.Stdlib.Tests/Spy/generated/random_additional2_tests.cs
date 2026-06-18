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
using static Sharpy.Stdlib.Tests.Spy.Random.RandomAdditional2Tests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Random
    {
        [global::Sharpy.SharpyModule("random.random_additional2_tests")]
        public static partial class RandomAdditional2Tests
        {
        }
    }

    public static partial class Random
    {
        public partial class RandomAdditional2TestsTests
        {
            [Xunit.FactAttribute]
            public void TestSeedProducesIdenticalSequenceMultipleValues()
            {
#line (20, 5) - (20, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(1234);
#line (21, 5) - (21, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> seq1 = new Sharpy.List<int>()
                {
                };
#line (22, 5) - (22, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int i = 0;
#line (23, 5) - (26, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < 5)
                {
#line (24, 9) - (24, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    seq1.Append(random.Randint(0, 1000));
#line (25, 9) - (25, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }

#line (26, 5) - (26, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(1234);
#line (27, 5) - (27, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> seq2 = new Sharpy.List<int>()
                {
                };
#line (28, 5) - (28, 10) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                i = 0;
#line (29, 5) - (32, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < 5)
                {
#line (30, 9) - (30, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    seq2.Append(random.Randint(0, 1000));
#line (31, 9) - (31, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }

#line (32, 5) - (32, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(seq2, seq1);
            }

            [Xunit.FactAttribute]
            public void TestSeedDifferentSeedsProduceDifferentSequences()
            {
#line (36, 5) - (36, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(1);
#line (37, 5) - (37, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                double a = random.NextDouble();
#line (38, 5) - (38, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(2);
#line (39, 5) - (39, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                double b = random.NextDouble();
#line (41, 5) - (41, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.NotEqual(b, a);
            }

            [Xunit.FactAttribute]
            public void TestRandrangeOddStepOnlyOddValues()
            {
#line (47, 5) - (47, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(99);
#line (48, 5) - (48, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int i = 0;
#line (49, 5) - (56, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < 100)
                {
#line (50, 9) - (50, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    int val = random.Randrange(1, 10, 2);
#line (51, 9) - (51, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.Equal(1, val % 2);
#line (52, 9) - (52, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.True(val >= 1);
#line (53, 9) - (53, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.True(val < 10);
#line (54, 9) - (54, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestRandrangeStepLargerThanWidthSingleValue()
            {
#line (59, 5) - (59, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (60, 5) - (60, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int i = 0;
#line (61, 5) - (67, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < 10)
                {
#line (62, 9) - (62, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.Equal(3, random.Randrange(3, 4, 5));
#line (63, 9) - (63, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestSampleKEqualsLenReturnsPermutation()
            {
#line (69, 5) - (69, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (70, 5) - (70, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> pop = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (71, 5) - (71, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> result = random.Sample(pop, 5);
#line (72, 5) - (72, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.Builtins.Len(result));
#line (73, 5) - (73, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted<int>(pop), global::Sharpy.Builtins.Sorted<int>(result));
            }

            [Xunit.FactAttribute]
            public void TestSampleSingleElementReturnsThatElement()
            {
#line (77, 5) - (77, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (78, 5) - (78, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> pop = new Sharpy.List<int>()
                {
                    42
                };
#line (79, 5) - (79, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> result = random.Sample(pop, 1);
#line (80, 5) - (80, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (81, 5) - (81, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(42, result[0]);
            }

            [Xunit.FactAttribute]
            public void TestSampleUniqueElementsNoDuplicates()
            {
#line (85, 5) - (85, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(7);
#line (86, 5) - (86, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> pop = new Sharpy.List<int>()
                {
                    10,
                    20,
                    30,
                    40,
                    50,
                    60,
                    70,
                    80,
                    90,
                    100
                };
#line (87, 5) - (87, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> result = random.Sample(pop, 7);
#line (88, 5) - (88, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.Set<int> seen = new global::Sharpy.Set<int>();
#line (89, 5) - (95, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                foreach (var __loopVar_0 in result)
                {
                    var item = __loopVar_0;
#line (90, 9) - (90, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.DoesNotContain(item, seen);
#line (91, 9) - (91, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    seen.Add(item);
                }
            }

            [Xunit.FactAttribute]
            public void TestRandintSameAAndBReturnsThatValue()
            {
#line (97, 5) - (97, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (98, 5) - (98, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int i = 0;
#line (99, 5) - (103, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < 10)
                {
#line (100, 9) - (100, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.Equal(7, random.Randint(7, 7));
#line (101, 9) - (101, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestRandintLargeRangeStaysInBounds()
            {
#line (105, 5) - (105, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (106, 5) - (106, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int lo = -1073741823;
#line (107, 5) - (107, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int hi = 1073741823;
#line (108, 5) - (108, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int i = 0;
#line (109, 5) - (117, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < 100)
                {
#line (110, 9) - (110, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    int val = random.Randint(lo, hi);
#line (111, 9) - (111, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.True(val >= lo);
#line (112, 9) - (112, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.True(val <= hi);
#line (113, 9) - (113, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestUniformAEqualsBReturnsA()
            {
#line (119, 5) - (119, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (120, 5) - (120, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                double val = random.Uniform(5.5d, 5.5d);
#line (121, 5) - (121, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(val - 5.5d) < 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestUniformBLessThanAReturnsInReversedRange()
            {
#line (125, 5) - (125, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (126, 5) - (126, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int i = 0;
#line (127, 5) - (136, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < 50)
                {
#line (128, 9) - (128, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    double val = random.Uniform(10.0d, 1.0d);
#line (130, 9) - (130, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.True(val >= 1.0d);
#line (131, 9) - (131, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.True(val <= 10.0d);
#line (132, 9) - (132, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestNextDoubleNeverReturns1()
            {
#line (138, 5) - (138, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (139, 5) - (139, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int i = 0;
#line (140, 5) - (144, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < 1000)
                {
#line (141, 9) - (141, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.True(random.NextDouble() < 1.0d);
#line (142, 9) - (142, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestNextDoubleStatisticalMeanApproximatesHalf()
            {
#line (146, 5) - (146, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (147, 5) - (147, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                double total = 0.0d;
#line (148, 5) - (148, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int n = 1000;
#line (149, 5) - (149, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int i = 0;
#line (150, 5) - (153, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < n)
                {
#line (151, 9) - (151, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    total = total + random.NextDouble();
#line (152, 9) - (152, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }

#line (153, 5) - (153, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(total / n - 0.5d) < 0.05d);
            }

            [Xunit.FactAttribute]
            public void TestGetrandbitsSixteenInRange()
            {
#line (159, 5) - (159, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (160, 5) - (160, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                int i = 0;
#line (161, 5) - (169, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                while (i < 100)
                {
#line (162, 9) - (162, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    int val = random.Getrandbits(16);
#line (163, 9) - (163, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (164, 9) - (164, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.True(val < 65536);
#line (165, 9) - (165, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestShuffleEmptyListDoesNotThrow()
            {
#line (171, 5) - (171, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
                {
                };
#line (172, 5) - (172, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Shuffle(lst);
#line (173, 5) - (173, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(lst));
            }

            [Xunit.FactAttribute]
            public void TestShuffleSingleElementUnchanged()
            {
#line (177, 5) - (177, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
                {
                    42
                };
#line (178, 5) - (178, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Shuffle(lst);
#line (179, 5) - (179, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(42, lst[0]);
            }

            [Xunit.FactAttribute]
            public void TestShufflePreservesAllElements()
            {
#line (183, 5) - (183, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (184, 5) - (184, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<string> lst = new Sharpy.List<string>()
                {
                    "a",
                    "b",
                    "c",
                    "d",
                    "e"
                };
#line (185, 5) - (185, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<string> original = new Sharpy.List<string>()
                {
                    "a",
                    "b",
                    "c",
                    "d",
                    "e"
                };
#line (186, 5) - (186, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Shuffle(lst);
#line (187, 5) - (187, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted<string>(original), global::Sharpy.Builtins.Sorted<string>(lst));
#line (188, 5) - (188, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Len(original), global::Sharpy.Builtins.Len(lst));
            }

            [Xunit.FactAttribute]
            public void TestGaussNegativeSigmaStillReturnsValue()
            {
#line (195, 5) - (195, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (196, 5) - (196, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                double val = random.Gauss(0.0d, -1.0d);
#line (197, 5) - (197, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(val, val);
            }

            [Xunit.FactAttribute]
            public void TestChoicesKOneReturnsSingleElement()
            {
#line (203, 5) - (203, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (204, 5) - (204, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> pop = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (205, 5) - (205, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> result = random.Choices(pop, k: 1);
#line (206, 5) - (206, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (207, 5) - (207, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Xunit.Assert.Contains(result[0], pop);
            }

            [Xunit.FactAttribute]
            public void TestChoicesAllSameElementAllResultsAreThatElement()
            {
#line (211, 5) - (211, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                random.Seed(42);
#line (212, 5) - (212, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> pop = new Sharpy.List<int>()
                {
                    7
                };
#line (213, 5) - (213, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                Sharpy.List<int> result = random.Choices(pop, k: 10);
#line (214, 5) - (216, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                foreach (var __loopVar_1 in result)
                {
                    var item = __loopVar_1;
#line (215, 9) - (215, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/random/random_additional2_tests.spy"
                    Xunit.Assert.Equal(7, item);
                }
            }
        }
    }
}
