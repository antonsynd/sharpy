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
#line (26, 5) - (26, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (27, 5) - (27, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d });
#line (28, 5) - (28, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (29, 5) - (29, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (30, 5) - (30, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestConcatenate2dAxis0()
            {
#line (34, 5) - (34, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (35, 5) - (35, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 7.0d, 8.0d, 9.0d }).Reshape(1, 3);
#line (36, 5) - (36, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 0);
#line (37, 5) - (37, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (38, 5) - (38, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (39, 5) - (39, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(7.0d, r[2, 0]);
#line (40, 5) - (40, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(9.0d, r[2, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestConcatenate2dAxis1()
            {
#line (44, 5) - (44, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (45, 5) - (45, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d }).Reshape(2, 1);
#line (46, 5) - (46, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 1);
#line (47, 5) - (47, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (48, 5) - (48, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (49, 5) - (49, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5.0d, r[0, 2]);
#line (50, 5) - (50, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestConcatenateMismatchedDimThrows()
            {
#line (54, 5) - (54, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Zeros(6).Reshape(2, 3);
#line (55, 5) - (55, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Zeros(4).Reshape(2, 2);
#line (56, 5) - (61, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
#line hidden
                {
#line (57, 9) - (57, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                    np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 0);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestStack1dAddsLeadingAxis()
            {
#line (63, 5) - (63, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (64, 5) - (64, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (65, 5) - (65, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Stack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (66, 5) - (66, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (67, 5) - (67, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (68, 5) - (68, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, r[0, 0]);
#line (69, 5) - (69, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStack1dAxis1()
            {
#line (73, 5) - (73, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (74, 5) - (74, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (75, 5) - (75, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Stack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 1);
#line (76, 5) - (76, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (77, 5) - (77, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (78, 5) - (78, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, r[0, 0]);
#line (79, 5) - (79, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(4.0d, r[0, 1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHstack1d()
            {
#line (83, 5) - (83, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d });
#line (84, 5) - (84, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 3.0d, 4.0d, 5.0d });
#line (85, 5) - (85, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Hstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (86, 5) - (86, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (87, 5) - (87, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHstack2d()
            {
#line (91, 5) - (91, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (92, 5) - (92, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d }).Reshape(2, 1);
#line (93, 5) - (93, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Hstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (94, 5) - (94, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (95, 5) - (95, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (96, 5) - (96, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5.0d, r[0, 2]);
#line (97, 5) - (97, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestVstack1d()
            {
#line (101, 5) - (101, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (102, 5) - (102, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (103, 5) - (103, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Vstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (104, 5) - (104, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (105, 5) - (105, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (106, 5) - (106, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, r[0, 0]);
#line (107, 5) - (107, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestVstack2d()
            {
#line (111, 5) - (111, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (112, 5) - (112, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d }).Reshape(1, 2);
#line (113, 5) - (113, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Vstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (114, 5) - (114, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (115, 5) - (115, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (116, 5) - (116, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(5.0d, r[2, 0]);
#line (117, 5) - (117, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, r[2, 1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplit1dSingleIndex()
            {
#line (123, 5) - (123, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (124, 5) - (124, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, (new Sharpy.List<int>() { 2 }).ToArray());
#line (125, 5) - (125, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (126, 5) - (126, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (127, 5) - (127, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, parts[0][0]);
#line (128, 5) - (128, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3.0d, parts[1][0]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplit1dMultipleIndices()
            {
#line (132, 5) - (132, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (133, 5) - (133, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, (new Sharpy.List<int>() { 2, 4 }).ToArray());
#line (134, 5) - (134, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (135, 5) - (135, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (136, 5) - (136, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[2].Shape, 0));
#line (137, 5) - (137, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, parts[0][0]);
#line (138, 5) - (138, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3.0d, parts[1][0]);
#line (139, 5) - (139, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, parts[2][1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplit2dAxis1()
            {
#line (143, 5) - (143, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d, 7.0d, 8.0d }).Reshape(2, 4);
#line (144, 5) - (144, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, (new Sharpy.List<int>() { 2 }).ToArray(), 1);
#line (145, 5) - (145, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (146, 5) - (146, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 1));
#line (147, 5) - (147, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (148, 5) - (148, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplitIntSections1d()
            {
#line (160, 5) - (160, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (161, 5) - (161, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, 3);
#line (162, 5) - (162, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(parts));
#line (163, 5) - (163, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (164, 5) - (164, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (165, 5) - (165, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[2].Shape, 0));
#line (166, 5) - (166, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1.0d, parts[0][0]);
#line (167, 5) - (167, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3.0d, parts[1][0]);
#line (168, 5) - (168, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, parts[2][1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplitIntSectionsOneReturnsWholeArray()
            {
#line (172, 5) - (172, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (173, 5) - (173, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, 1);
#line (174, 5) - (174, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(parts));
#line (175, 5) - (175, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplitIntSections2dAxis1()
            {
#line (179, 5) - (179, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d, 7.0d, 8.0d }).Reshape(2, 4);
#line (180, 5) - (180, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, 2, 1);
#line (181, 5) - (181, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(parts));
#line (182, 5) - (182, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (183, 5) - (183, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 1));
#line (184, 5) - (184, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplitIntSectionsUnevenRaises()
            {
#line (188, 5) - (188, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (189, 5) - (192, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (190, 9) - (190, 23) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                    np.Split(a, 4);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestSplitIntSectionsNonPositiveRaises()
            {
#line (194, 5) - (194, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (195, 5) - (198, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (196, 9) - (196, 23) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                    np.Split(a, 0);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestSplitIndicesFormStillBindsTheIndicesOverload()
            {
#line (204, 5) - (204, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (205, 5) - (205, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, (new Sharpy.List<int>() { 2, 4 }).ToArray());
#line (206, 5) - (206, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(parts));
#line (207, 5) - (207, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (208, 5) - (208, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (209, 5) - (209, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[2].Shape, 0));
#line (210, 5) - (210, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(6.0d, parts[2][1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWhereDerivedFromComparison()
            {
#line (216, 5) - (216, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { -1.0d, 2.0d, -3.0d, 4.0d });
#line (217, 5) - (217, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var zero = np.Array(new Sharpy.List<double>() { 0.0d });
#line (218, 5) - (218, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var pos = np.Greater(a, zero);
#line (219, 5) - (219, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Where(pos, a, zero);
#line (220, 5) - (220, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(0.0d, r[0]);
#line (221, 5) - (221, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2.0d, r[1]);
#line (222, 5) - (222, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(0.0d, r[2]);
#line (223, 5) - (223, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(4.0d, r[3]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestClipClampsBetweenMinAndMax()
            {
#line (229, 5) - (229, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { -2.0d, -1.0d, 0.0d, 1.0d, 2.0d, 3.0d });
#line (230, 5) - (230, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Clip(a, 0.0d, 2.0d);
#line (231, 5) - (231, 69) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 0.0d, 0.0d, 1.0d, 2.0d, 2.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestClipAllAboveMax()
            {
#line (235, 5) - (235, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d, 7.0d });
#line (236, 5) - (236, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Clip(a, 0.0d, 1.0d);
#line (237, 5) - (237, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 1.0d, 1.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestClipPreservesShape()
            {
#line (241, 5) - (241, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { -5.0d, 0.0d, 5.0d, 10.0d }).Reshape(2, 2);
#line (242, 5) - (242, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var r = np.Clip(a, 0.0d, 5.0d);
#line (243, 5) - (243, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (244, 5) - (244, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestClipMinGreaterThanMaxThrows()
            {
#line (248, 5) - (248, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d });
#line (249, 5) - (253, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
#line hidden
                {
#line (250, 9) - (250, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                    np.Clip(a, 5.0d, 2.0d);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestSplitReturnsASharpyList()
            {
#line (258, 5) - (258, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (259, 5) - (259, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var parts = np.Split(a, (new Sharpy.List<int>() { 2 }).ToArray());
#line (260, 5) - (260, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(parts));
#line (261, 5) - (261, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                var extra = np.Array(new Sharpy.List<double>() { 9.0d });
#line (262, 5) - (262, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                parts.Append(extra);
#line (263, 5) - (263, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(parts));
#line (264, 5) - (264, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                Xunit.Assert.Equal(9.0d, parts[2][0]);
#line hidden
            }
        }
    }
}
#line default
