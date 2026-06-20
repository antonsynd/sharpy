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
using static Sharpy.Stdlib.Tests.Spy.Numpy.NdarrayIndexingTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.ndarray_indexing_tests")]
        public static partial class NdarrayIndexingTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NdarrayIndexingTestsTests
        {
            [Xunit.FactAttribute]
            public void TestIndexer1dPositiveReads()
            {
#line (23, 5) - (23, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d, 40.0d });
#line (24, 5) - (24, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(10.0d, arr[0]);
#line (25, 5) - (25, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(40.0d, arr[3]);
            }

            [Xunit.FactAttribute]
            public void TestIndexer1dNegativeCountsFromEnd()
            {
#line (29, 5) - (29, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d, 40.0d });
#line (30, 5) - (30, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(40.0d, arr[-1]);
#line (31, 5) - (31, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(10.0d, arr[-4]);
            }

            [Xunit.FactAttribute]
            public void TestIndexer1dOutOfRangeThrows()
            {
#line (35, 5) - (35, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (36, 5) - (39, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (37, 9) - (37, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                    var _ = arr[3];
                }));
            }

            [Xunit.FactAttribute]
            public void TestIndexer1dNegativeOutOfRangeThrows()
            {
#line (41, 5) - (41, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (42, 5) - (47, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (43, 9) - (43, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                    var _ = arr[-4];
                }));
            }

            [Xunit.FactAttribute]
            public void TestIndexer2dReadsCorrectValues()
            {
#line (52, 5) - (52, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (53, 5) - (53, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(1.0d, arr[0, 0]);
#line (54, 5) - (54, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(3.0d, arr[0, 2]);
#line (55, 5) - (55, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(4.0d, arr[1, 0]);
#line (56, 5) - (56, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(6.0d, arr[1, 2]);
            }

            [Xunit.FactAttribute]
            public void TestIndexer2dNegativeCountsFromEnd()
            {
#line (60, 5) - (60, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (61, 5) - (61, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(6.0d, arr[-1, -1]);
#line (62, 5) - (62, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(1.0d, arr[-2, -3]);
            }

            [Xunit.FactAttribute]
            public void TestIndexer2dOutOfRangeThrows()
            {
#line (66, 5) - (66, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (67, 5) - (70, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (68, 9) - (68, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                    var _ = arr[2, 0];
                }));
            }

            [Xunit.FactAttribute]
            public void TestIndexer2dWrongIndexCountThrows()
            {
#line (72, 5) - (72, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (73, 5) - (78, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (74, 9) - (74, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                    var _ = arr[1];
                }));
            }

            [Xunit.FactAttribute]
            public void TestIndexer3dReadsCorrectValues()
            {
#line (81, 5) - (81, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Arange(0.0d, 12.0d, 1.0d).Reshape(2, 2, 3);
#line (82, 5) - (82, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(0.0d, arr[0, 0, 0]);
#line (83, 5) - (83, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(2.0d, arr[0, 0, 2]);
#line (84, 5) - (84, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(3.0d, arr[0, 1, 0]);
#line (85, 5) - (85, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(6.0d, arr[1, 0, 0]);
#line (86, 5) - (86, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(11.0d, arr[1, 1, 2]);
            }

            [Xunit.FactAttribute]
            public void TestIndexer3dNegativeCountsFromEnd()
            {
#line (90, 5) - (90, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                var arr = np.Arange(0.0d, 12.0d, 1.0d).Reshape(2, 2, 3);
#line (91, 5) - (91, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_indexing_tests.spy"
                Xunit.Assert.Equal(11.0d, arr[-1, -1, -1]);
            }
        }
    }
}
