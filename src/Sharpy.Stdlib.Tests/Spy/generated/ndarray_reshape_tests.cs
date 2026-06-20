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
using static Sharpy.Stdlib.Tests.Spy.Numpy.NdarrayReshapeTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.ndarray_reshape_tests")]
        public static partial class NdarrayReshapeTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NdarrayReshapeTestsTests
        {
            [Xunit.FactAttribute]
            public void TestReshape1dTo2d()
            {
#line (30, 5) - (30, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (31, 5) - (31, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var reshaped = arr.Reshape(2, 3);
#line (32, 5) - (32, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2, reshaped.Shape[0]);
#line (33, 5) - (33, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3, reshaped.Shape[1]);
#line (34, 5) - (34, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(1.0d, reshaped[0, 0]);
#line (35, 5) - (35, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2.0d, reshaped[0, 1]);
#line (36, 5) - (36, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3.0d, reshaped[0, 2]);
#line (37, 5) - (37, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(4.0d, reshaped[1, 0]);
#line (38, 5) - (38, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(6.0d, reshaped[1, 2]);
            }

            [Xunit.FactAttribute]
            public void TestReshape2dTo1d()
            {
#line (42, 5) - (42, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (43, 5) - (43, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var reshaped = arr.Reshape(4);
#line (44, 5) - (44, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(4, reshaped.Shape[0]);
#line (45, 5) - (45, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(1.0d, reshaped[0]);
#line (46, 5) - (46, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(4.0d, reshaped[3]);
            }

            [Xunit.FactAttribute]
            public void TestReshapeInferredDimFirst()
            {
#line (50, 5) - (50, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (51, 5) - (51, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var reshaped = arr.Reshape(-1, 3);
#line (52, 5) - (52, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2, reshaped.Shape[0]);
#line (53, 5) - (53, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3, reshaped.Shape[1]);
            }

            [Xunit.FactAttribute]
            public void TestReshapeInferredDimLast()
            {
#line (57, 5) - (57, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (58, 5) - (58, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var reshaped = arr.Reshape(2, -1);
#line (59, 5) - (59, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2, reshaped.Shape[0]);
#line (60, 5) - (60, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3, reshaped.Shape[1]);
            }

            [Xunit.FactAttribute]
            public void TestReshapeIncompatibleSizeThrows()
            {
#line (64, 5) - (64, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (65, 5) - (68, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
                {
#line (66, 9) - (66, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                    arr.Reshape(3, 2);
                }));
            }

            [Xunit.FactAttribute]
            public void TestReshapeMultipleInferredDimsThrows()
            {
#line (70, 5) - (70, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (71, 5) - (74, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
                {
#line (72, 9) - (72, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                    arr.Reshape(-1, -1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestReshapeInferredNotDivisibleThrows()
            {
#line (76, 5) - (76, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (77, 5) - (82, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
                {
#line (78, 9) - (78, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                    arr.Reshape(-1, 2);
                }));
            }

            [Xunit.FactAttribute]
            public void TestTranspose2dReversesDimensions()
            {
#line (87, 5) - (87, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (88, 5) - (88, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var t = arr.Transpose();
#line (89, 5) - (89, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3, t.Shape[0]);
#line (90, 5) - (90, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2, t.Shape[1]);
#line (91, 5) - (91, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(1.0d, t[0, 0]);
#line (92, 5) - (92, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(4.0d, t[0, 1]);
#line (93, 5) - (93, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2.0d, t[1, 0]);
#line (94, 5) - (94, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(5.0d, t[1, 1]);
#line (95, 5) - (95, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3.0d, t[2, 0]);
#line (96, 5) - (96, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(6.0d, t[2, 1]);
            }

            [Xunit.FactAttribute]
            public void TestTranspose3dReversesAllAxes()
            {
#line (100, 5) - (100, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Arange(0.0d, 24.0d).Reshape(2, 3, 4);
#line (101, 5) - (101, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var t = arr.Transpose();
#line (102, 5) - (102, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(4, t.Shape[0]);
#line (103, 5) - (103, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3, t.Shape[1]);
#line (104, 5) - (104, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2, t.Shape[2]);
            }

            [Xunit.FactAttribute]
            public void TestTranspose1dNoChange()
            {
#line (108, 5) - (108, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (109, 5) - (109, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var t = arr.Transpose();
#line (110, 5) - (110, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3, t.Shape[0]);
#line (111, 5) - (111, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(1.0d, t[0]);
#line (112, 5) - (112, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3.0d, t[2]);
            }

            [Xunit.FactAttribute]
            public void TestFlatten2dReturns1d()
            {
#line (118, 5) - (118, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (119, 5) - (119, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var flat = arr.Flatten();
#line (120, 5) - (120, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(1, flat.Ndim);
#line (121, 5) - (121, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(6, flat.Shape[0]);
#line (122, 5) - (122, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.True(np.Allclose(flat, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d })));
            }

            [Xunit.FactAttribute]
            public void TestFlattenOfTransposeTraversesRowMajor()
            {
#line (126, 5) - (126, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (127, 5) - (127, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var flat = arr.Transpose().Flatten();
#line (130, 5) - (130, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(6, flat.Shape[0]);
#line (131, 5) - (131, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.True(np.Allclose(flat, np.Array(new Sharpy.List<double>() { 1.0d, 4.0d, 2.0d, 5.0d, 3.0d, 6.0d })));
            }

            [Xunit.FactAttribute]
            public void TestRavelContiguousReturns1d()
            {
#line (137, 5) - (137, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (138, 5) - (138, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var rav = arr.Ravel();
#line (139, 5) - (139, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(1, rav.Ndim);
#line (140, 5) - (140, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(4, rav.Shape[0]);
#line (141, 5) - (141, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.True(np.Allclose(rav, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d })));
            }

            [Xunit.FactAttribute]
            public void TestRavelNonContiguousReturns1d()
            {
#line (145, 5) - (145, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (146, 5) - (146, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var rav = arr.Transpose().Ravel();
#line (147, 5) - (147, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(6, rav.Shape[0]);
#line (149, 5) - (149, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.True(np.Allclose(rav, np.Array(new Sharpy.List<double>() { 1.0d, 4.0d, 2.0d, 5.0d, 3.0d, 6.0d })));
            }

            [Xunit.FactAttribute]
            public void TestCopySameShapePreservesValues()
            {
#line (155, 5) - (155, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (156, 5) - (156, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var c = arr.Copy();
#line (157, 5) - (157, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2, c.Shape[0]);
#line (158, 5) - (158, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2, c.Shape[1]);
#line (159, 5) - (159, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(1.0d, c[0, 0]);
#line (160, 5) - (160, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.True(np.Allclose(c.Flatten(), arr.Flatten()));
            }

            [Xunit.FactAttribute]
            public void TestCopyOfViewIsContiguousCopy()
            {
#line (164, 5) - (164, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (165, 5) - (165, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                var c = arr.Transpose().Copy();
#line (166, 5) - (166, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(3, c.Shape[0]);
#line (167, 5) - (167, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(2, c.Shape[1]);
#line (168, 5) - (168, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(1.0d, c[0, 0]);
#line (169, 5) - (169, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(4.0d, c[0, 1]);
#line (170, 5) - (170, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_reshape_tests.spy"
                Xunit.Assert.Equal(6.0d, c[2, 1]);
            }
        }
    }
}
