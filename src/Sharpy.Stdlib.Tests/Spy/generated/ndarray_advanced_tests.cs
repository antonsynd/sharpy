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
using math = global::Sharpy.MathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Numpy.NdarrayAdvancedTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.ndarray_advanced_tests")]
        public static partial class NdarrayAdvancedTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NdarrayAdvancedTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSortReturnsAscendingCopy()
            {
#line (25, 5) - (25, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 3.0d, 1.0d, 4.0d, 1.0d, 5.0d, 9.0d });
#line (26, 5) - (26, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var result = np.Sort(arr);
#line (27, 5) - (27, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.True(np.Allclose(result, np.Array(new Sharpy.List<double>() { 1.0d, 1.0d, 3.0d, 4.0d, 5.0d, 9.0d })));
#line (29, 5) - (29, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 3.0d, 1.0d, 4.0d, 1.0d, 5.0d, 9.0d })));
            }

            [Xunit.FactAttribute]
            public void TestArgsortReturnsSortingIndices()
            {
#line (35, 5) - (35, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 30.0d, 10.0d, 20.0d });
#line (36, 5) - (36, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var indices = np.Argsort(arr);
#line (37, 5) - (37, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(3, indices.Size);
#line (38, 5) - (38, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(1, indices[0]);
#line (39, 5) - (39, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(2, indices[1]);
#line (40, 5) - (40, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(0, indices[2]);
            }

            [Xunit.FactAttribute]
            public void TestUniqueReturnsSortedDistinct()
            {
#line (46, 5) - (46, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 3.0d, 1.0d, 2.0d, 1.0d, 3.0d, 2.0d });
#line (47, 5) - (47, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var result = np.Unique(arr);
#line (48, 5) - (48, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(3, result.Size);
#line (49, 5) - (49, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.True(np.Allclose(result, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d })));
            }

            [Xunit.FactAttribute]
            public void TestSearchsortedReturnsInsertionIndices()
            {
#line (55, 5) - (55, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var sortedArr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 4.0d, 5.0d });
#line (56, 5) - (56, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var values = np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 3.0d, 6.0d });
#line (57, 5) - (57, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var result = np.Searchsorted(sortedArr, values);
#line (58, 5) - (58, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(4, result.Size);
#line (59, 5) - (59, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(0, result[0]);
#line (60, 5) - (60, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(0, result[1]);
#line (61, 5) - (61, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(2, result[2]);
#line (62, 5) - (62, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(4, result[3]);
            }

            [Xunit.FactAttribute]
            public void TestAllcloseNearbyValues()
            {
#line (68, 5) - (68, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (69, 5) - (69, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 1.0000000001d, 1.9999999999d, 3.0d });
#line (70, 5) - (70, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.True(np.Allclose(a, b));
            }

            [Xunit.FactAttribute]
            public void TestAllcloseDivergentValues()
            {
#line (74, 5) - (74, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (75, 5) - (75, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.5d, 3.0d });
#line (76, 5) - (76, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.False(np.Allclose(a, b));
            }

            [Xunit.FactAttribute]
            public void TestAllcloseBothNan()
            {
#line (80, 5) - (80, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { math.Nan });
#line (81, 5) - (81, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { math.Nan });
#line (82, 5) - (82, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.True(np.Allclose(a, b));
            }

            [Xunit.FactAttribute]
            public void TestAllcloseWithBroadcasting()
            {
#line (86, 5) - (86, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (87, 5) - (87, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var scalar = np.Array(new Sharpy.List<double>() { 1.0d });
#line (88, 5) - (88, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.False(np.Allclose(a, scalar));
            }

            [Xunit.FactAttribute]
            public void TestIsnanFlagsNanValues()
            {
#line (94, 5) - (94, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, math.Nan, 3.0d, math.Nan });
#line (95, 5) - (95, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var result = np.Isnan(arr);
#line (96, 5) - (96, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(4, result.Size);
            }

            [Xunit.FactAttribute]
            public void TestIsinfFlagsInfinities()
            {
#line (100, 5) - (100, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, math.Inf, -math.Inf, 4.0d });
#line (101, 5) - (101, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                var result = np.Isinf(arr);
#line (102, 5) - (102, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_advanced_tests.spy"
                Xunit.Assert.Equal(4, result.Size);
            }
        }
    }
}
