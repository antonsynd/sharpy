// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

#pragma warning disable xUnit2009, xUnit2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using np = global::Sharpy.Numpy;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Numpy.NdarraySlicingTests
{
    [global::Sharpy.SharpyModule("numpy.ndarray_slicing_tests")]
    public static partial class NdarraySlicingTestsModule
    {
    }

    public partial class NdarraySlicingTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestSlice1dBasicReturnsView()
        {
#line (21, 5) - (21, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (22, 5) - (22, 15) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var v = a.Slice(new global::Sharpy.SliceSpec((int?)1, (int?)4));
#line (23, 5) - (23, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(1, v.Ndim);
#line (24, 5) - (24, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(v.Shape, 0));
#line (25, 5) - (25, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.True(np.Allclose(v, np.Array(new Sharpy.List<double>() { 2.0d, 3.0d, 4.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSlice1dViewSharesBuffer()
        {
#line (29, 5) - (29, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (30, 5) - (30, 15) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var v = a.Slice(new global::Sharpy.SliceSpec((int?)1, (int?)4));
#line (31, 5) - (31, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            v[0] = 99.0d;
#line (32, 5) - (32, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(99.0d, a[1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSlice1dNullStartStop()
        {
#line (36, 5) - (36, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (37, 5) - (37, 13) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var v = a.Slice(global::Sharpy.SliceSpec.All);
#line (38, 5) - (38, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(v.Shape, 0));
#line (39, 5) - (39, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.True(np.Allclose(v, a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSlice1dStep()
        {
#line (43, 5) - (43, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (44, 5) - (44, 15) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var v = a.Slice(new global::Sharpy.SliceSpec(null, null, (int?)2));
#line (45, 5) - (45, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(v.Shape, 0));
#line (46, 5) - (46, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.True(np.Allclose(v, np.Array(new Sharpy.List<double>() { 1.0d, 3.0d, 5.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSlice1dNegativeStep()
        {
#line (50, 5) - (50, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (51, 5) - (51, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var v = a.Slice(new global::Sharpy.SliceSpec(null, null, (int?)-1));
#line (52, 5) - (52, 64) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.True(np.Allclose(v, np.Array(new Sharpy.List<double>() { 5.0d, 4.0d, 3.0d, 2.0d, 1.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSliceViewHasUpdatedStrides()
        {
#line (56, 5) - (56, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (57, 5) - (57, 15) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var v = a.Slice(new global::Sharpy.SliceSpec(null, null, (int?)2));
#line (58, 5) - (58, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.ArrayHelpers.GetItem(a.Strides, 0) * 2, global::Sharpy.ArrayHelpers.GetItem(v.Strides, 0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSliceWrongSliceCountThrows()
        {
#line (62, 5) - (62, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var m = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (63, 5) - (64, 15) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (64, 9) - (64, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
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
#line (68, 5) - (68, 78) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var m = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d, 7.0d, 8.0d, 9.0d }).Reshape(3, 3);
#line (69, 5) - (69, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var sub = m.Slice(new global::Sharpy.SliceSpec((int?)0, (int?)2), new global::Sharpy.SliceSpec((int?)1, (int?)3));
#line (70, 5) - (70, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(sub.Shape, 0));
#line (71, 5) - (71, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(sub.Shape, 1));
#line (72, 5) - (72, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(2.0d, sub[0, 0]);
#line (73, 5) - (73, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(3.0d, sub[0, 1]);
#line (74, 5) - (74, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(5.0d, sub[1, 0]);
#line (75, 5) - (75, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(6.0d, sub[1, 1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetRowReturnsView()
        {
#line (81, 5) - (81, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (82, 5) - (82, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var row = arr.GetRow(1);
#line (83, 5) - (83, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(1, row.Ndim);
#line (84, 5) - (84, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(3, row.Shape[0]);
#line (85, 5) - (85, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.True(np.Allclose(row, np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetRowNegative()
        {
#line (89, 5) - (89, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (90, 5) - (90, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var row = arr.GetRow(-1);
#line (91, 5) - (91, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.True(np.Allclose(row, np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetRowModifiesOriginal()
        {
#line (95, 5) - (95, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (96, 5) - (96, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var row = arr.GetRow(0);
#line (97, 5) - (97, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            row[0] = 99.0d;
#line (98, 5) - (98, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(99.0d, arr[0, 0]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetRowNot2dThrows()
        {
#line (102, 5) - (102, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (103, 5) - (104, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (104, 9) - (104, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                arr.GetRow(0);
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
        public void TestGetRowOutOfRangeThrows()
        {
#line (108, 5) - (108, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (109, 5) - (110, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (110, 9) - (110, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
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
#line (116, 5) - (116, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (117, 5) - (117, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var col = arr.GetColumn(1);
#line (118, 5) - (118, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(1, col.Ndim);
#line (119, 5) - (119, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(2, col.Shape[0]);
#line (120, 5) - (120, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.True(np.Allclose(col, np.Array(new Sharpy.List<double>() { 2.0d, 5.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetColumnNegative()
        {
#line (124, 5) - (124, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (125, 5) - (125, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var col = arr.GetColumn(-1);
#line (126, 5) - (126, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.True(np.Allclose(col, np.Array(new Sharpy.List<double>() { 3.0d, 6.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetColumnModifiesOriginal()
        {
#line (130, 5) - (130, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (131, 5) - (131, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var col = arr.GetColumn(2);
#line (132, 5) - (132, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            col[1] = 99.0d;
#line (133, 5) - (133, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            Xunit.Assert.Equal(99.0d, arr[1, 2]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetColumnNot2dThrows()
        {
#line (137, 5) - (137, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (138, 5) - (139, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            bool __raised_3 = false;
#line hidden
            try
            {
#line (139, 9) - (139, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
                arr.GetColumn(0);
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
        public void TestGetColumnOutOfRangeThrows()
        {
#line (143, 5) - (143, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (144, 5) - (145, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
            bool __raised_4 = false;
#line hidden
            try
            {
#line (145, 9) - (145, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_slicing_tests.spy"
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
#line default
