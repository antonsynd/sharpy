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
using static Sharpy.Stdlib.Tests.Spy.Numpy.NdarrayOperatorTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.ndarray_operator_tests")]
        public static partial class NdarrayOperatorTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NdarrayOperatorTestsTests
        {
            [Xunit.FactAttribute]
            public void TestAddSameShape1d()
            {
#line (23, 5) - (23, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (24, 5) - (24, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (25, 5) - (25, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = a + b;
#line (26, 5) - (26, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[0]);
#line (27, 5) - (27, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 5.0d, 7.0d, 9.0d })));
            }

            [Xunit.FactAttribute]
            public void TestSubtractSameShape2d()
            {
#line (31, 5) - (31, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d, 40.0d }).Reshape(2, 2);
#line (32, 5) - (32, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (33, 5) - (33, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = a - b;
#line (34, 5) - (34, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[0]);
#line (35, 5) - (35, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[1]);
#line (36, 5) - (36, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r.Flatten(), np.Array(new Sharpy.List<double>() { 9.0d, 18.0d, 27.0d, 36.0d })));
            }

            [Xunit.FactAttribute]
            public void TestMultiplySameShape()
            {
#line (40, 5) - (40, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 2.0d, 3.0d, 4.0d });
#line (41, 5) - (41, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d, 7.0d });
#line (42, 5) - (42, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = a * b;
#line (43, 5) - (43, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 10.0d, 18.0d, 28.0d })));
            }

            [Xunit.FactAttribute]
            public void TestDivideSameShape()
            {
#line (47, 5) - (47, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d });
#line (48, 5) - (48, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 2.0d, 4.0d, 5.0d });
#line (49, 5) - (49, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = a / b;
#line (50, 5) - (50, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 5.0d, 5.0d, 6.0d })));
            }

            [Xunit.FactAttribute]
            public void TestAddScalarRight()
            {
#line (56, 5) - (56, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (57, 5) - (57, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = a + 10.0d;
#line (58, 5) - (58, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 11.0d, 12.0d, 13.0d })));
            }

            [Xunit.FactAttribute]
            public void TestAddScalarLeft()
            {
#line (62, 5) - (62, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (63, 5) - (63, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = 10.0d + a;
#line (64, 5) - (64, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 11.0d, 12.0d, 13.0d })));
            }

            [Xunit.FactAttribute]
            public void TestMultiplyScalarRight()
            {
#line (68, 5) - (68, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (69, 5) - (69, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = a * 2.0d;
#line (70, 5) - (70, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 2.0d, 4.0d, 6.0d, 8.0d })));
            }

            [Xunit.FactAttribute]
            public void TestSubtractScalarLeft()
            {
#line (74, 5) - (74, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (75, 5) - (75, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = 10.0d - a;
#line (76, 5) - (76, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 9.0d, 8.0d, 7.0d })));
            }

            [Xunit.FactAttribute]
            public void TestDivideScalarLeft()
            {
#line (80, 5) - (80, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 4.0d });
#line (81, 5) - (81, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = 8.0d / a;
#line (82, 5) - (82, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 8.0d, 4.0d, 2.0d })));
            }

            [Xunit.FactAttribute]
            public void TestNegateFlipsSigns()
            {
#line (88, 5) - (88, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, -2.0d, 3.0d });
#line (89, 5) - (89, 11) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = -a;
#line (90, 5) - (90, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { -1.0d, 2.0d, -3.0d })));
            }

            [Xunit.FactAttribute]
            public void TestNegatePreservesShape()
            {
#line (94, 5) - (94, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (95, 5) - (95, 11) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = -a;
#line (96, 5) - (96, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.Equal(2, r.Shape[0]);
#line (97, 5) - (97, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[1]);
            }

            [Xunit.FactAttribute]
            public void TestAddBroadcast1dToMatrix()
            {
#line (103, 5) - (103, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (104, 5) - (104, 138) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d, 40.0d, 50.0d, 100.0d, 200.0d, 300.0d, 400.0d, 500.0d, 1000.0d, 2000.0d, 3000.0d, 4000.0d, 5000.0d }).Reshape(3, 5);
#line (105, 5) - (105, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var r = a + b;
#line (106, 5) - (106, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.Equal(3, r.Shape[0]);
#line (107, 5) - (107, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.Equal(5, r.Shape[1]);
#line (108, 5) - (108, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var row0 = r.GetRow(0);
#line (109, 5) - (109, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.True(np.Allclose(row0, np.Array(new Sharpy.List<double>() { 11.0d, 22.0d, 33.0d, 44.0d, 55.0d })));
            }

            [Xunit.FactAttribute]
            public void TestAddIncompatibleShapesThrows()
            {
#line (113, 5) - (113, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (114, 5) - (114, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (115, 5) - (118, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
                {
#line (116, 9) - (116, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                    var _ = a + b;
                }));
            }

            [Xunit.FactAttribute]
            public void TestAddIncompatibleMatricesThrows()
            {
#line (120, 5) - (120, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var a = np.Zeros(6).Reshape(2, 3);
#line (121, 5) - (121, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                var b = np.Zeros(12).Reshape(4, 3);
#line (122, 5) - (124, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
                {
#line (123, 9) - (123, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_operator_tests.spy"
                    var _ = a + b;
                }));
            }
        }
    }
}
