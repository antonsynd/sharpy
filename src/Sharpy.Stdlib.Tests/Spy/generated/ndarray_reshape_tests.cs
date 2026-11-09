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

namespace Sharpy.Stdlib.Tests.Spy.Numpy.NdarrayReshapeTests
{
    [global::Sharpy.SharpyModule("numpy.ndarray_reshape_tests")]
    public static partial class NdarrayReshapeTestsModule
    {
    }

    public partial class NdarrayReshapeTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestReshape1dTo2d()
        {
#line (29, 5) - (29, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (30, 5) - (30, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var reshaped = arr.Reshape(2, 3);
#line (31, 5) - (31, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(reshaped.Shape, 0));
#line (32, 5) - (32, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(reshaped.Shape, 1));
#line (33, 5) - (33, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(1.0d, reshaped[0, 0]);
#line (34, 5) - (34, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2.0d, reshaped[0, 1]);
#line (35, 5) - (35, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3.0d, reshaped[0, 2]);
#line (36, 5) - (36, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(4.0d, reshaped[1, 0]);
#line (37, 5) - (37, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(6.0d, reshaped[1, 2]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestReshape2dTo1d()
        {
#line (41, 5) - (41, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (42, 5) - (42, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var reshaped = arr.Reshape(4);
#line (43, 5) - (43, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(4, global::Sharpy.ArrayHelpers.GetItem(reshaped.Shape, 0));
#line (44, 5) - (44, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(1.0d, reshaped[0]);
#line (45, 5) - (45, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(4.0d, reshaped[3]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestReshapeInferredDimFirst()
        {
#line (49, 5) - (49, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (50, 5) - (50, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var reshaped = arr.Reshape(-1, 3);
#line (51, 5) - (51, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(reshaped.Shape, 0));
#line (52, 5) - (52, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(reshaped.Shape, 1));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestReshapeInferredDimLast()
        {
#line (56, 5) - (56, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (57, 5) - (57, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var reshaped = arr.Reshape(2, -1);
#line (58, 5) - (58, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(reshaped.Shape, 0));
#line (59, 5) - (59, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(reshaped.Shape, 1));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestReshapeIncompatibleSizeThrows()
        {
#line (63, 5) - (63, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (64, 5) - (65, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (65, 9) - (65, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                arr.Reshape(3, 2);
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
        public void TestReshapeMultipleInferredDimsThrows()
        {
#line (69, 5) - (69, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (70, 5) - (71, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (71, 9) - (71, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                arr.Reshape(-1, -1);
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
        public void TestReshapeInferredNotDivisibleThrows()
        {
#line (75, 5) - (75, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (76, 5) - (77, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (77, 9) - (77, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                arr.Reshape(-1, 2);
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
        public void TestTranspose2dReversesDimensions()
        {
#line (86, 5) - (86, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (87, 5) - (87, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var t = arr.Transpose();
#line (88, 5) - (88, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(t.Shape, 0));
#line (89, 5) - (89, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(t.Shape, 1));
#line (90, 5) - (90, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(1.0d, t[0, 0]);
#line (91, 5) - (91, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(4.0d, t[0, 1]);
#line (92, 5) - (92, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2.0d, t[1, 0]);
#line (93, 5) - (93, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(5.0d, t[1, 1]);
#line (94, 5) - (94, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3.0d, t[2, 0]);
#line (95, 5) - (95, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(6.0d, t[2, 1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTranspose3dReversesAllAxes()
        {
#line (99, 5) - (99, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Arange(0.0d, 24.0d).Reshape(2, 3, 4);
#line (100, 5) - (100, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var t = arr.Transpose();
#line (101, 5) - (101, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(4, global::Sharpy.ArrayHelpers.GetItem(t.Shape, 0));
#line (102, 5) - (102, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(t.Shape, 1));
#line (103, 5) - (103, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(t.Shape, 2));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTranspose1dNoChange()
        {
#line (107, 5) - (107, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (108, 5) - (108, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var t = arr.Transpose();
#line (109, 5) - (109, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(t.Shape, 0));
#line (110, 5) - (110, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(1.0d, t[0]);
#line (111, 5) - (111, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3.0d, t[2]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestFlatten2dReturns1d()
        {
#line (117, 5) - (117, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (118, 5) - (118, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var flat = arr.Flatten();
#line (119, 5) - (119, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(1, flat.Ndim);
#line (120, 5) - (120, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(6, global::Sharpy.ArrayHelpers.GetItem(flat.Shape, 0));
#line (121, 5) - (121, 72) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.True(np.Allclose(flat, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestFlattenOfTransposeTraversesRowMajor()
        {
#line (125, 5) - (125, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (126, 5) - (126, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var flat = arr.Transpose().Flatten();
#line (129, 5) - (129, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(6, global::Sharpy.ArrayHelpers.GetItem(flat.Shape, 0));
#line (130, 5) - (130, 72) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.True(np.Allclose(flat, np.Array(new Sharpy.List<double>() { 1.0d, 4.0d, 2.0d, 5.0d, 3.0d, 6.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRavelContiguousReturns1d()
        {
#line (136, 5) - (136, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (137, 5) - (137, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var rav = arr.Ravel();
#line (138, 5) - (138, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(1, rav.Ndim);
#line (139, 5) - (139, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(4, global::Sharpy.ArrayHelpers.GetItem(rav.Shape, 0));
#line (140, 5) - (140, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.True(np.Allclose(rav, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRavelNonContiguousReturns1d()
        {
#line (144, 5) - (144, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (145, 5) - (145, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var rav = arr.Transpose().Ravel();
#line (146, 5) - (146, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(6, global::Sharpy.ArrayHelpers.GetItem(rav.Shape, 0));
#line (148, 5) - (148, 71) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.True(np.Allclose(rav, np.Array(new Sharpy.List<double>() { 1.0d, 4.0d, 2.0d, 5.0d, 3.0d, 6.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCopySameShapePreservesValues()
        {
#line (154, 5) - (154, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (155, 5) - (155, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var c = arr.Copy();
#line (156, 5) - (156, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(c.Shape, 0));
#line (157, 5) - (157, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(c.Shape, 1));
#line (158, 5) - (158, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(1.0d, c[0, 0]);
#line (159, 5) - (159, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.True(np.Allclose(c.Flatten(), arr.Flatten()));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCopyOfViewIsContiguousCopy()
        {
#line (163, 5) - (163, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (164, 5) - (164, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            var c = arr.Transpose().Copy();
#line (165, 5) - (165, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(c.Shape, 0));
#line (166, 5) - (166, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(c.Shape, 1));
#line (167, 5) - (167, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(1.0d, c[0, 0]);
#line (168, 5) - (168, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(4.0d, c[0, 1]);
#line (169, 5) - (169, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
            Xunit.Assert.Equal(6.0d, c[2, 1]);
#line hidden
        }
    }
}
#line default
