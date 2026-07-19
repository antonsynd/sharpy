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
using static Sharpy.Stdlib.Tests.Spy.Numpy.NumpyManipulationTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.numpy_manipulation_tests")]
        public static partial class NumpyManipulationTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NumpyManipulationTestsTests
        {
            [Xunit.FactAttribute]
            public void TestConcatenate1d()
            {
#line (25, 5) - (25, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (26, 5) - (26, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d });
#line (27, 5) - (27, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (28, 5) - (28, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5, r.Shape[0]);
#line (29, 5) - (29, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestConcatenate2dAxis0()
            {
#line (33, 5) - (33, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (34, 5) - (34, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 7.0d, 8.0d, 9.0d }).Reshape(1, 3);
#line (35, 5) - (35, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 0);
#line (36, 5) - (36, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[0]);
#line (37, 5) - (37, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[1]);
#line (38, 5) - (38, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(7.0d, r[2, 0]);
#line (39, 5) - (39, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(9.0d, r[2, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestConcatenate2dAxis1()
            {
#line (43, 5) - (43, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (44, 5) - (44, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d }).Reshape(2, 1);
#line (45, 5) - (45, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 1);
#line (46, 5) - (46, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[0]);
#line (47, 5) - (47, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[1]);
#line (48, 5) - (48, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5.0d, r[0, 2]);
#line (49, 5) - (49, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestConcatenateMismatchedDimThrows()
            {
#line (53, 5) - (53, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Zeros(6).Reshape(2, 3);
#line (54, 5) - (54, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Zeros(4).Reshape(2, 2);
#line (55, 5) - (60, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
#line hidden
                {
#line (56, 9) - (56, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                    np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 0);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestStack1dAddsLeadingAxis()
            {
#line (62, 5) - (62, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (63, 5) - (63, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (64, 5) - (64, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Stack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (65, 5) - (65, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[0]);
#line (66, 5) - (66, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[1]);
#line (67, 5) - (67, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, r[0, 0]);
#line (68, 5) - (68, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStack1dAxis1()
            {
#line (72, 5) - (72, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (73, 5) - (73, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (74, 5) - (74, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Stack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 1);
#line (75, 5) - (75, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[0]);
#line (76, 5) - (76, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[1]);
#line (77, 5) - (77, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, r[0, 0]);
#line (78, 5) - (78, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(4.0d, r[0, 1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHstack1d()
            {
#line (82, 5) - (82, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d });
#line (83, 5) - (83, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 3.0d, 4.0d, 5.0d });
#line (84, 5) - (84, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Hstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (85, 5) - (85, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5, r.Shape[0]);
#line (86, 5) - (86, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHstack2d()
            {
#line (90, 5) - (90, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (91, 5) - (91, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d }).Reshape(2, 1);
#line (92, 5) - (92, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Hstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (93, 5) - (93, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[0]);
#line (94, 5) - (94, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[1]);
#line (95, 5) - (95, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5.0d, r[0, 2]);
#line (96, 5) - (96, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestVstack1d()
            {
#line (100, 5) - (100, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (101, 5) - (101, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (102, 5) - (102, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Vstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (103, 5) - (103, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[0]);
#line (104, 5) - (104, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[1]);
#line (105, 5) - (105, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, r[0, 0]);
#line (106, 5) - (106, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestVstack2d()
            {
#line (110, 5) - (110, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (111, 5) - (111, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d }).Reshape(1, 2);
#line (112, 5) - (112, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Vstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (113, 5) - (113, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[0]);
#line (114, 5) - (114, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[1]);
#line (115, 5) - (115, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5.0d, r[2, 0]);
#line (116, 5) - (116, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[2, 1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplit1dSingleIndex()
            {
#line (122, 5) - (122, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (123, 5) - (123, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, (new Sharpy.List<int>() { 2 }).ToArray());
#line (124, 5) - (124, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts, 0).Shape[0]);
#line (125, 5) - (125, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(parts, 1).Shape[0]);
#line (126, 5) - (126, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, global::Sharpy.ArrayHelpers.GetItem(parts, 0)[0]);
#line (127, 5) - (127, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3.0d, global::Sharpy.ArrayHelpers.GetItem(parts, 1)[0]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplit1dMultipleIndices()
            {
#line (131, 5) - (131, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (132, 5) - (132, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, (new Sharpy.List<int>() { 2, 4 }).ToArray());
#line (133, 5) - (133, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts, 0).Shape[0]);
#line (134, 5) - (134, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts, 1).Shape[0]);
#line (135, 5) - (135, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts, 2).Shape[0]);
#line (136, 5) - (136, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, global::Sharpy.ArrayHelpers.GetItem(parts, 0)[0]);
#line (137, 5) - (137, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3.0d, global::Sharpy.ArrayHelpers.GetItem(parts, 1)[0]);
#line (138, 5) - (138, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, global::Sharpy.ArrayHelpers.GetItem(parts, 2)[1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplit2dAxis1()
            {
#line (142, 5) - (142, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d, 7.0d, 8.0d }).Reshape(2, 4);
#line (143, 5) - (143, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, (new Sharpy.List<int>() { 2 }).ToArray(), 1);
#line (144, 5) - (144, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts, 0).Shape[0]);
#line (145, 5) - (145, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts, 0).Shape[1]);
#line (146, 5) - (146, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts, 1).Shape[0]);
#line (147, 5) - (147, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts, 1).Shape[1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWhereDerivedFromComparison()
            {
#line (153, 5) - (153, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { -1.0d, 2.0d, -3.0d, 4.0d });
#line (154, 5) - (154, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var zero = np.Array(new Sharpy.List<double>() { 0.0d });
#line (155, 5) - (155, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var pos = np.Greater(a, zero);
#line (156, 5) - (156, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Where(pos, a, zero);
#line (157, 5) - (157, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(0.0d, r[0]);
#line (158, 5) - (158, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2.0d, r[1]);
#line (159, 5) - (159, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(0.0d, r[2]);
#line (160, 5) - (160, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(4.0d, r[3]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestClipClampsBetweenMinAndMax()
            {
#line (166, 5) - (166, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { -2.0d, -1.0d, 0.0d, 1.0d, 2.0d, 3.0d });
#line (167, 5) - (167, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Clip(a, 0.0d, 2.0d);
#line (168, 5) - (168, 69) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 0.0d, 0.0d, 1.0d, 2.0d, 2.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestClipAllAboveMax()
            {
#line (172, 5) - (172, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d, 7.0d });
#line (173, 5) - (173, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Clip(a, 0.0d, 1.0d);
#line (174, 5) - (174, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 1.0d, 1.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestClipPreservesShape()
            {
#line (178, 5) - (178, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { -5.0d, 0.0d, 5.0d, 10.0d }).Reshape(2, 2);
#line (179, 5) - (179, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Clip(a, 0.0d, 5.0d);
#line (180, 5) - (180, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[0]);
#line (181, 5) - (181, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestClipMinGreaterThanMaxThrows()
            {
#line (185, 5) - (185, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d });
#line (186, 5) - (188, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
#line hidden
                {
#line (187, 9) - (187, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                    np.Clip(a, 5.0d, 2.0d);
#line hidden
                }));
            }
        }
    }
}
#line default
