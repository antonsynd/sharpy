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

namespace Sharpy.Stdlib.Tests.Spy.Numpy.NumpyManipulationTests
{
    [global::Sharpy.SharpyModule("numpy.numpy_manipulation_tests")]
    public static partial class NumpyManipulationTestsModule
    {
    }

    public partial class NumpyManipulationTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestConcatenate1d()
        {
#line (25, 5) - (25, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (26, 5) - (26, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d });
#line (27, 5) - (27, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (28, 5) - (28, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (29, 5) - (29, 64) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConcatenate2dAxis0()
        {
#line (33, 5) - (33, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (34, 5) - (34, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 7.0d, 8.0d, 9.0d }).Reshape(1, 3);
#line (35, 5) - (35, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 0);
#line (36, 5) - (36, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (37, 5) - (37, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (38, 5) - (38, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(7.0d, r[2, 0]);
#line (39, 5) - (39, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(9.0d, r[2, 2]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConcatenate2dAxis1()
        {
#line (43, 5) - (43, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (44, 5) - (44, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d }).Reshape(2, 1);
#line (45, 5) - (45, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 1);
#line (46, 5) - (46, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (47, 5) - (47, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (48, 5) - (48, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(5.0d, r[0, 2]);
#line (49, 5) - (49, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConcatenateMismatchedDimThrows()
        {
#line (53, 5) - (53, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Zeros(6).Reshape(2, 3);
#line (54, 5) - (54, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Zeros(4).Reshape(2, 2);
#line (55, 5) - (56, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (56, 9) - (56, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                np.Concatenate((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 0);
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
        public void TestStack1dAddsLeadingAxis()
        {
#line (62, 5) - (62, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (63, 5) - (63, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (64, 5) - (64, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Stack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (65, 5) - (65, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (66, 5) - (66, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (67, 5) - (67, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(1.0d, r[0, 0]);
#line (68, 5) - (68, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStack1dAxis1()
        {
#line (72, 5) - (72, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (73, 5) - (73, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (74, 5) - (74, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Stack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray(), 1);
#line (75, 5) - (75, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (76, 5) - (76, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (77, 5) - (77, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(1.0d, r[0, 0]);
#line (78, 5) - (78, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(4.0d, r[0, 1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHstack1d()
        {
#line (82, 5) - (82, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d });
#line (83, 5) - (83, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 3.0d, 4.0d, 5.0d });
#line (84, 5) - (84, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Hstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (85, 5) - (85, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (86, 5) - (86, 64) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHstack2d()
        {
#line (90, 5) - (90, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (91, 5) - (91, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d }).Reshape(2, 1);
#line (92, 5) - (92, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Hstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (93, 5) - (93, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (94, 5) - (94, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (95, 5) - (95, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(5.0d, r[0, 2]);
#line (96, 5) - (96, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVstack1d()
        {
#line (100, 5) - (100, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (101, 5) - (101, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (102, 5) - (102, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Vstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (103, 5) - (103, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (104, 5) - (104, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (105, 5) - (105, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(1.0d, r[0, 0]);
#line (106, 5) - (106, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(6.0d, r[1, 2]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVstack2d()
        {
#line (110, 5) - (110, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (111, 5) - (111, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d }).Reshape(1, 2);
#line (112, 5) - (112, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Vstack((new Sharpy.List<global::Sharpy.NdArray<double>>() { a, b }).ToArray());
#line (113, 5) - (113, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (114, 5) - (114, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (115, 5) - (115, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(5.0d, r[2, 0]);
#line (116, 5) - (116, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(6.0d, r[2, 1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplit1dSingleIndex()
        {
#line (122, 5) - (122, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (123, 5) - (123, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var parts = np.Split(a, (new Sharpy.List<int>() { 2 }).ToArray());
#line (124, 5) - (124, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (125, 5) - (125, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (126, 5) - (126, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(1.0d, parts[0][0]);
#line (127, 5) - (127, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3.0d, parts[1][0]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplit1dMultipleIndices()
        {
#line (131, 5) - (131, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (132, 5) - (132, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var parts = np.Split(a, (new Sharpy.List<int>() { 2, 4 }).ToArray());
#line (133, 5) - (133, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (134, 5) - (134, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (135, 5) - (135, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[2].Shape, 0));
#line (136, 5) - (136, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(1.0d, parts[0][0]);
#line (137, 5) - (137, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3.0d, parts[1][0]);
#line (138, 5) - (138, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(6.0d, parts[2][1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplit2dAxis1()
        {
#line (142, 5) - (142, 73) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d, 7.0d, 8.0d }).Reshape(2, 4);
#line (143, 5) - (143, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var parts = np.Split(a, (new Sharpy.List<int>() { 2 }).ToArray(), 1);
#line (144, 5) - (144, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (145, 5) - (145, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 1));
#line (146, 5) - (146, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (147, 5) - (147, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 1));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitIntSections1d()
        {
#line (159, 5) - (159, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (160, 5) - (160, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var parts = np.Split(a, 3);
#line (161, 5) - (161, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(parts));
#line (162, 5) - (162, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (163, 5) - (163, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (164, 5) - (164, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[2].Shape, 0));
#line (165, 5) - (165, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(1.0d, parts[0][0]);
#line (166, 5) - (166, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3.0d, parts[1][0]);
#line (167, 5) - (167, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(6.0d, parts[2][1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitIntSectionsOneReturnsWholeArray()
        {
#line (171, 5) - (171, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (172, 5) - (172, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var parts = np.Split(a, 1);
#line (173, 5) - (173, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(parts));
#line (174, 5) - (174, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitIntSections2dAxis1()
        {
#line (178, 5) - (178, 73) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d, 7.0d, 8.0d }).Reshape(2, 4);
#line (179, 5) - (179, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var parts = np.Split(a, 2, 1);
#line (180, 5) - (180, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(parts));
#line (181, 5) - (181, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (182, 5) - (182, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 1));
#line (183, 5) - (183, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 1));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitIntSectionsUnevenRaises()
        {
#line (187, 5) - (187, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (188, 5) - (189, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (189, 9) - (189, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                np.Split(a, 4);
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
        public void TestSplitIntSectionsNonPositiveRaises()
        {
#line (193, 5) - (193, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (194, 5) - (195, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (195, 9) - (195, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
                np.Split(a, 0);
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
        public void TestSplitIndicesFormStillBindsTheIndicesOverload()
        {
#line (203, 5) - (203, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d });
#line (204, 5) - (204, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var parts = np.Split(a, (new Sharpy.List<int>() { 2, 4 }).ToArray());
#line (205, 5) - (205, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(parts));
#line (206, 5) - (206, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[0].Shape, 0));
#line (207, 5) - (207, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[1].Shape, 0));
#line (208, 5) - (208, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(parts[2].Shape, 0));
#line (209, 5) - (209, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(6.0d, parts[2][1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestWhereDerivedFromComparison()
        {
#line (215, 5) - (215, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { -1.0d, 2.0d, -3.0d, 4.0d });
#line (216, 5) - (216, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var zero = np.Array(new Sharpy.List<double>() { 0.0d });
#line (217, 5) - (217, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var pos = np.Greater(a, zero);
#line (218, 5) - (218, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Where(pos, a, zero);
#line (219, 5) - (219, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(0.0d, r[0]);
#line (220, 5) - (220, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2.0d, r[1]);
#line (221, 5) - (221, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(0.0d, r[2]);
#line (222, 5) - (222, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(4.0d, r[3]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestClipClampsBetweenMinAndMax()
        {
#line (228, 5) - (228, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { -2.0d, -1.0d, 0.0d, 1.0d, 2.0d, 3.0d });
#line (229, 5) - (229, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Clip(a, 0.0d, 2.0d);
#line (230, 5) - (230, 69) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 0.0d, 0.0d, 1.0d, 2.0d, 2.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestClipAllAboveMax()
        {
#line (234, 5) - (234, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d, 7.0d });
#line (235, 5) - (235, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Clip(a, 0.0d, 1.0d);
#line (236, 5) - (236, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 1.0d, 1.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestClipPreservesShape()
        {
#line (240, 5) - (240, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { -5.0d, 0.0d, 5.0d, 10.0d }).Reshape(2, 2);
#line (241, 5) - (241, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Clip(a, 0.0d, 5.0d);
#line (242, 5) - (242, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (243, 5) - (243, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestClipMinGreaterThanMaxReturnsMax()
        {
#line (249, 5) - (249, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 3.0d, 7.0d });
#line (250, 5) - (250, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var r = np.Clip(a, 5.0d, 2.0d);
#line (251, 5) - (251, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 2.0d, 2.0d, 2.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitReturnsASharpyList()
        {
#line (259, 5) - (259, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (260, 5) - (260, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var parts = np.Split(a, (new Sharpy.List<int>() { 2 }).ToArray());
#line (261, 5) - (261, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(parts));
#line (262, 5) - (262, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            var extra = np.Array(new Sharpy.List<double>() { 9.0d });
#line (263, 5) - (263, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            parts.Append(extra);
#line (264, 5) - (264, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(parts));
#line (265, 5) - (265, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_manipulation_tests.spy"
            Xunit.Assert.Equal(9.0d, parts[2][0]);
#line hidden
        }
    }
}
#line default
