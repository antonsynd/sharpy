// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using np = global::Sharpy.Numpy;
using static global::Sharpy.Unittest;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Numpy.NumpyRandomTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.numpy_random_tests")]
        public static partial class NumpyRandomTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NumpyRandomTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSeedProducesReproducibleSequence()
            {
#line (22, 5) - (22, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Seed(42);
#line (23, 5) - (23, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var a = global::Sharpy.NumpyRandom.Rand(5);
#line (24, 5) - (24, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Seed(42);
#line (25, 5) - (25, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var b = global::Sharpy.NumpyRandom.Rand(5);
#line (26, 5) - (26, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(np.Allclose(a, b));
            }

            [Xunit.FactAttribute]
            public void TestRandProducesValuesInUnitInterval()
            {
#line (32, 5) - (32, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Seed(7);
#line (33, 5) - (33, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var arr = global::Sharpy.NumpyRandom.Rand(100);
#line (34, 5) - (34, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Equal(100, arr.Shape[0]);
#line (35, 5) - (35, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(arr[0] >= 0.0d);
#line (36, 5) - (36, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(arr[0] < 1.0d);
#line (37, 5) - (37, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(arr[99] >= 0.0d);
#line (38, 5) - (38, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(arr[99] < 1.0d);
            }

            [Xunit.FactAttribute]
            public void TestRandMultiDimensionalShape()
            {
#line (42, 5) - (42, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Seed(3);
#line (43, 5) - (43, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var arr = global::Sharpy.NumpyRandom.Rand(2, 3, 4);
#line (44, 5) - (44, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Equal(2, arr.Shape[0]);
#line (45, 5) - (45, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Equal(3, arr.Shape[1]);
#line (46, 5) - (46, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Equal(4, arr.Shape[2]);
#line (47, 5) - (47, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Equal(24, arr.Size);
            }

            [Xunit.FactAttribute]
            public void TestRandnHasApproximatelyZeroMean()
            {
#line (53, 5) - (53, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Seed(123);
#line (54, 5) - (54, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var arr = global::Sharpy.NumpyRandom.Randn(10000);
#line (55, 5) - (55, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var total = np.Sum(arr);
#line (56, 5) - (56, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var mean = total / arr.Size;
#line (57, 5) - (57, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(mean) < 0.05d);
            }

            [Xunit.FactAttribute]
            public void TestRandintProducesValuesInRange()
            {
#line (63, 5) - (63, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Seed(11);
#line (64, 5) - (64, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var arr = global::Sharpy.NumpyRandom.Randint(5, 10, (new Sharpy.List<int>() { 200 }).ToArray());
#line (65, 5) - (65, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Equal(200, arr.Size);
#line (66, 5) - (66, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(arr[0] >= 5);
#line (67, 5) - (67, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(arr[0] < 10);
            }

            [Xunit.FactAttribute]
            public void TestRandintThrowsWhenHighEqualsLow()
            {
#line (71, 5) - (76, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (72, 9) - (72, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                    global::Sharpy.NumpyRandom.Randint(5, 5, (new Sharpy.List<int>() { 4 }).ToArray());
                }));
            }

            [Xunit.FactAttribute]
            public void TestNormalRespectsMeanAndScale()
            {
#line (78, 5) - (78, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Seed(99);
#line (79, 5) - (79, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var arr = global::Sharpy.NumpyRandom.Normal(10.0d, 2.0d, (new Sharpy.List<int>() { 10000 }).ToArray());
#line (80, 5) - (80, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var total = np.Sum(arr);
#line (81, 5) - (81, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var mean = total / arr.Size;
#line (82, 5) - (82, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(mean - 10.0d) < 0.2d);
            }

            [Xunit.FactAttribute]
            public void TestNormalThrowsWhenScaleNegative()
            {
#line (86, 5) - (91, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (87, 9) - (87, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                    global::Sharpy.NumpyRandom.Normal(0.0d, -1.0d, (new Sharpy.List<int>() { 5 }).ToArray());
                }));
            }

            [Xunit.FactAttribute]
            public void TestUniformProducesValuesInRange()
            {
#line (93, 5) - (93, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Seed(8);
#line (94, 5) - (94, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var arr = global::Sharpy.NumpyRandom.Uniform(-3.0d, 5.0d, (new Sharpy.List<int>() { 500 }).ToArray());
#line (95, 5) - (95, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Equal(500, arr.Size);
#line (96, 5) - (96, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(arr[0] >= -3.0d);
#line (97, 5) - (97, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(arr[0] < 5.0d);
            }

            [Xunit.FactAttribute]
            public void TestUniformThrowsWhenHighLessThanLow()
            {
#line (101, 5) - (106, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (102, 9) - (102, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                    global::Sharpy.NumpyRandom.Uniform(5.0d, 0.0d, (new Sharpy.List<int>() { 4 }).ToArray());
                }));
            }

            [Xunit.FactAttribute]
            public void TestShufflePreservesElements()
            {
#line (108, 5) - (108, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Seed(21);
#line (109, 5) - (109, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d, 7.0d, 8.0d });
#line (110, 5) - (110, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                var originalSorted = np.Sort(arr.Copy());
#line (111, 5) - (111, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                global::Sharpy.NumpyRandom.Shuffle(arr);
#line (112, 5) - (112, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_random_tests.spy"
                Xunit.Assert.True(np.Allclose(np.Sort(arr), originalSorted));
            }
        }
    }
}
