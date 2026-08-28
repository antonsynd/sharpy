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
            public void TestSlice1dBasicReturnsView()
            {
#line (22, 5) - (22, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (23, 5) - (23, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var v = a.Slice(new global::Sharpy.SliceSpec((int?)1, (int?)4));
#line (24, 5) - (24, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(1, v.Ndim);
#line (25, 5) - (25, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(v.Shape, 0));
#line (26, 5) - (26, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(v, np.Array(new Sharpy.List<double>() { 2.0d, 3.0d, 4.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSlice1dViewSharesBuffer()
            {
#line (30, 5) - (30, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (31, 5) - (31, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var v = a.Slice(new global::Sharpy.SliceSpec((int?)1, (int?)4));
#line (32, 5) - (32, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                v[0] = 99.0d;
#line (33, 5) - (33, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(99.0d, a[1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSlice1dNullStartStop()
            {
#line (37, 5) - (37, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (38, 5) - (38, 13) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var v = a.Slice(global::Sharpy.SliceSpec.All);
#line (39, 5) - (39, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(v.Shape, 0));
#line (40, 5) - (40, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(v, a));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSlice1dStep()
            {
#line (44, 5) - (44, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (45, 5) - (45, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var v = a.Slice(new global::Sharpy.SliceSpec(null, null, (int?)2));
#line (46, 5) - (46, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(v.Shape, 0));
#line (47, 5) - (47, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(v, np.Array(new Sharpy.List<double>() { 1.0d, 3.0d, 5.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSlice1dNegativeStep()
            {
#line (51, 5) - (51, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (52, 5) - (52, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var v = a.Slice(new global::Sharpy.SliceSpec(null, null, (int?)-1));
#line (53, 5) - (53, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(v, np.Array(new Sharpy.List<double>() { 5.0d, 4.0d, 3.0d, 2.0d, 1.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSliceViewHasUpdatedStrides()
            {
#line (57, 5) - (57, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (58, 5) - (58, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var v = a.Slice(new global::Sharpy.SliceSpec(null, null, (int?)2));
#line (59, 5) - (59, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.ArrayHelpers.GetItem(a.Strides, 0) * 2, global::Sharpy.ArrayHelpers.GetItem(v.Strides, 0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSliceWrongSliceCountThrows()
            {
#line (63, 5) - (63, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var m = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (64, 5) - (67, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (65, 9) - (65, 15) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                    _ = m.Slice(new global::Sharpy.SliceSpec((int?)0, (int?)2));
#line hidden
                }
                catch (IndexError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected IndexError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestSlice2dBothAxesReturnsSubMatrix()
            {
#line (69, 5) - (69, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var m = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d, 7.0d, 8.0d, 9.0d }).Reshape(3, 3);
#line (70, 5) - (70, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var sub = m.Slice(new global::Sharpy.SliceSpec((int?)0, (int?)2), new global::Sharpy.SliceSpec((int?)1, (int?)3));
#line (71, 5) - (71, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(sub.Shape, 0));
#line (72, 5) - (72, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(sub.Shape, 1));
#line (73, 5) - (73, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(2.0d, sub[0, 0]);
#line (74, 5) - (74, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(3.0d, sub[0, 1]);
#line (75, 5) - (75, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(5.0d, sub[1, 0]);
#line (76, 5) - (76, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(6.0d, sub[1, 1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetRowReturnsView()
            {
#line (82, 5) - (82, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (83, 5) - (83, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var row = arr.GetRow(1);
#line (84, 5) - (84, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(1, row.Ndim);
#line (85, 5) - (85, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(3, row.Shape[0]);
#line (86, 5) - (86, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(row, np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetRowNegative()
            {
#line (90, 5) - (90, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (91, 5) - (91, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var row = arr.GetRow(-1);
#line (92, 5) - (92, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(row, np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetRowModifiesOriginal()
            {
#line (96, 5) - (96, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (97, 5) - (97, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var row = arr.GetRow(0);
#line (98, 5) - (98, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                row[0] = 99.0d;
#line (99, 5) - (99, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(99.0d, arr[0, 0]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetRowNot2dThrows()
            {
#line (103, 5) - (103, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (104, 5) - (107, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (105, 9) - (105, 22) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                    arr.GetRow(0);
#line hidden
                }
                catch (InvalidOperationException)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected InvalidOperationException to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGetRowOutOfRangeThrows()
            {
#line (109, 5) - (109, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (110, 5) - (115, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (111, 9) - (111, 22) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                    arr.GetRow(5);
#line hidden
                }
                catch (IndexError)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected IndexError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGetColumnReturnsView()
            {
#line (117, 5) - (117, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (118, 5) - (118, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var col = arr.GetColumn(1);
#line (119, 5) - (119, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(1, col.Ndim);
#line (120, 5) - (120, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(2, col.Shape[0]);
#line (121, 5) - (121, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(col, np.Array(new Sharpy.List<double>() { 2.0d, 5.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetColumnNegative()
            {
#line (125, 5) - (125, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (126, 5) - (126, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var col = arr.GetColumn(-1);
#line (127, 5) - (127, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.True(np.Allclose(col, np.Array(new Sharpy.List<double>() { 3.0d, 6.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetColumnModifiesOriginal()
            {
#line (131, 5) - (131, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (132, 5) - (132, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var col = arr.GetColumn(2);
#line (133, 5) - (133, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                col[1] = 99.0d;
#line (134, 5) - (134, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                Xunit.Assert.Equal(99.0d, arr[1, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetColumnNot2dThrows()
            {
#line (138, 5) - (138, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (139, 5) - (142, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (140, 9) - (140, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                    arr.GetColumn(0);
#line hidden
                }
                catch (InvalidOperationException)
                {
                    __raised_3 = true;
                }

                if (!__raised_3)
                    throw new global::Sharpy.AssertionError("Expected InvalidOperationException to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGetColumnOutOfRangeThrows()
            {
#line (144, 5) - (144, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (145, 5) - (147, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                bool __raised_4 = false;
#line hidden
                try
                {
#line (146, 9) - (146, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                    arr.GetColumn(5);
#line hidden
                }
                catch (IndexError)
                {
                    __raised_4 = true;
                }

                if (!__raised_4)
                    throw new global::Sharpy.AssertionError("Expected IndexError to be raised, but no exception was raised");
            }
        }
    }
}
#line default
