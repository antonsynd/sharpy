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
using static Sharpy.Stdlib.Tests.Spy.Numpy.NdarraySlicingTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.ndarray_slicing_tests")]
        public static partial class NdarraySlicingTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NdarraySlicingTestsTests
        {
            [Xunit.FactAttribute]
            public void TestGetRowReturnsView()
            {
#line (27, 5) - (27, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (28, 5) - (28, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var row = arr.GetRow(1);
#line (29, 5) - (29, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(1, row.Ndim);
#line (30, 5) - (30, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(3, row.Shape[0]);
#line (31, 5) - (31, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(row, np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d })));
            }

            [Xunit.FactAttribute]
            public void TestGetRowNegative()
            {
#line (35, 5) - (35, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (36, 5) - (36, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var row = arr.GetRow(-1);
#line (37, 5) - (37, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(row, np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d })));
            }

            [Xunit.FactAttribute]
            public void TestGetRowModifiesOriginal()
            {
#line (41, 5) - (41, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (42, 5) - (42, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var row = arr.GetRow(0);
#line (43, 5) - (43, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                row[0] = 99.0d;
#line (44, 5) - (44, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(99.0d, arr[0, 0]);
            }

            [Xunit.FactAttribute]
            public void TestGetRowNot2dThrows()
            {
#line (48, 5) - (48, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (49, 5) - (52, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Throws<InvalidOperationException>((global::System.Action)(() =>
                {
#line (50, 9) - (50, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                    arr.GetRow(0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetRowOutOfRangeThrows()
            {
#line (54, 5) - (54, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (55, 5) - (60, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (56, 9) - (56, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                    arr.GetRow(5);
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetColumnReturnsView()
            {
#line (62, 5) - (62, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (63, 5) - (63, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var col = arr.GetColumn(1);
#line (64, 5) - (64, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(1, col.Ndim);
#line (65, 5) - (65, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(2, col.Shape[0]);
#line (66, 5) - (66, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(col, np.Array(new Sharpy.List<double>() { 2.0d, 5.0d })));
            }

            [Xunit.FactAttribute]
            public void TestGetColumnNegative()
            {
#line (70, 5) - (70, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (71, 5) - (71, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var col = arr.GetColumn(-1);
#line (72, 5) - (72, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(col, np.Array(new Sharpy.List<double>() { 3.0d, 6.0d })));
            }

            [Xunit.FactAttribute]
            public void TestGetColumnModifiesOriginal()
            {
#line (76, 5) - (76, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (77, 5) - (77, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var col = arr.GetColumn(2);
#line (78, 5) - (78, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                col[1] = 99.0d;
#line (79, 5) - (79, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(99.0d, arr[1, 2]);
            }

            [Xunit.FactAttribute]
            public void TestGetColumnNot2dThrows()
            {
#line (83, 5) - (83, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (84, 5) - (87, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Throws<InvalidOperationException>((global::System.Action)(() =>
                {
#line (85, 9) - (85, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                    arr.GetColumn(0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetColumnOutOfRangeThrows()
            {
#line (89, 5) - (89, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (90, 5) - (92, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (91, 9) - (91, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                    arr.GetColumn(5);
                }));
            }
        }
    }
}
