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

namespace Sharpy.Stdlib.Tests.Spy.Numpy.NdarrayOperatorTests
{
    [global::Sharpy.SharpyModule("numpy.ndarray_operator_tests")]
    public static partial class NdarrayOperatorTestsModule
    {
    }

    public partial class NdarrayOperatorTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestAddSameShape1d()
        {
#line (22, 5) - (22, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (23, 5) - (23, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (24, 5) - (24, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = a + b;
#line (25, 5) - (25, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (26, 5) - (26, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 5.0d, 7.0d, 9.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSubtractSameShape2d()
        {
#line (30, 5) - (30, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d, 40.0d }).Reshape(2, 2);
#line (31, 5) - (31, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (32, 5) - (32, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = a - b;
#line (33, 5) - (33, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (34, 5) - (34, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (35, 5) - (35, 72) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r.Flatten(), np.Array(new Sharpy.List<double>() { 9.0d, 18.0d, 27.0d, 36.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMultiplySameShape()
        {
#line (39, 5) - (39, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 2.0d, 3.0d, 4.0d });
#line (40, 5) - (40, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d, 7.0d });
#line (41, 5) - (41, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = a * b;
#line (42, 5) - (42, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 10.0d, 18.0d, 28.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDivideSameShape()
        {
#line (46, 5) - (46, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d });
#line (47, 5) - (47, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 2.0d, 4.0d, 5.0d });
#line (48, 5) - (48, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = a / b;
#line (49, 5) - (49, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 5.0d, 5.0d, 6.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAddScalarRight()
        {
#line (55, 5) - (55, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (56, 5) - (56, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = a + 10.0d;
#line (57, 5) - (57, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 11.0d, 12.0d, 13.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAddScalarLeft()
        {
#line (61, 5) - (61, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (62, 5) - (62, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = 10.0d + a;
#line (63, 5) - (63, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 11.0d, 12.0d, 13.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMultiplyScalarRight()
        {
#line (67, 5) - (67, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (68, 5) - (68, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = a * 2.0d;
#line (69, 5) - (69, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 2.0d, 4.0d, 6.0d, 8.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSubtractScalarLeft()
        {
#line (73, 5) - (73, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (74, 5) - (74, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = 10.0d - a;
#line (75, 5) - (75, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 9.0d, 8.0d, 7.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDivideScalarLeft()
        {
#line (79, 5) - (79, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 4.0d });
#line (80, 5) - (80, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = 8.0d / a;
#line (81, 5) - (81, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 8.0d, 4.0d, 2.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNegateFlipsSigns()
        {
#line (87, 5) - (87, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, -2.0d, 3.0d });
#line (88, 5) - (88, 11) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = -a;
#line (89, 5) - (89, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { -1.0d, 2.0d, -3.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNegatePreservesShape()
        {
#line (93, 5) - (93, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (94, 5) - (94, 11) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = -a;
#line (95, 5) - (95, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (96, 5) - (96, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAddBroadcast1dToMatrix()
        {
#line (102, 5) - (102, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (103, 5) - (103, 138) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d, 40.0d, 50.0d, 100.0d, 200.0d, 300.0d, 400.0d, 500.0d, 1000.0d, 2000.0d, 3000.0d, 4000.0d, 5000.0d }).Reshape(3, 5);
#line (104, 5) - (104, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var r = a + b;
#line (105, 5) - (105, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (106, 5) - (106, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 1));
#line (107, 5) - (107, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var row0 = r.GetRow(0);
#line (108, 5) - (108, 72) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            Xunit.Assert.True(np.Allclose(row0, np.Array(new Sharpy.List<double>() { 11.0d, 22.0d, 33.0d, 44.0d, 55.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAddIncompatibleShapesThrows()
        {
#line (112, 5) - (112, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (113, 5) - (113, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (114, 5) - (115, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (115, 9) - (115, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var __spy_underscore = a + b;
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
        public void TestAddIncompatibleMatricesThrows()
        {
#line (119, 5) - (119, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var a = np.Zeros(6).Reshape(2, 3);
#line (120, 5) - (120, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            var b = np.Zeros(12).Reshape(4, 3);
#line (121, 5) - (122, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (122, 9) - (122, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var __spy_underscore = a + b;
#line hidden
            }
            catch (ValueError)
            {
                __raised_1 = true;
            }

            if (!__raised_1)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }
    }
}
#line default
