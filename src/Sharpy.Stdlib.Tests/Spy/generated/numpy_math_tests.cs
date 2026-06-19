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
using math = global::Sharpy.MathModule;
using static global::Sharpy.Unittest;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Numpy.NumpyMathTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.numpy_math_tests")]
        public static partial class NumpyMathTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NumpyMathTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSqrtArray()
            {
#line (36, 5) - (36, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 4.0d, 9.0d, 16.0d });
#line (37, 5) - (37, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Sqrt(a);
#line (38, 5) - (38, 86) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d, 4.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestSqrtScalar()
            {
#line (42, 5) - (42, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(5.0d, np.Sqrt(25.0d));
#line (43, 5) - (43, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(math.Sqrt(2.0d), np.Sqrt(2.0d));
            }

            [Xunit.FactAttribute]
            public void TestExpArray()
            {
#line (47, 5) - (47, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, -1.0d });
#line (48, 5) - (48, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Exp(a);
#line (49, 5) - (49, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var expected = np.Array(new Sharpy.List<double>() { math.Exp(0.0d), math.Exp(1.0d), math.Exp(2.0d), math.Exp(-1.0d) });
#line (50, 5) - (50, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, expected, rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestLogArray()
            {
#line (54, 5) - (54, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, math.E, 10.0d });
#line (55, 5) - (55, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Log(a);
#line (56, 5) - (56, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, math.Log(10.0d) }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestLog2Array()
            {
#line (60, 5) - (60, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 4.0d, 8.0d });
#line (61, 5) - (61, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Log2(a);
#line (62, 5) - (62, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestLog10Array()
            {
#line (66, 5) - (66, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 10.0d, 100.0d, 1000.0d });
#line (67, 5) - (67, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Log10(a);
#line (68, 5) - (68, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestAbsArray()
            {
#line (72, 5) - (72, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { -1.0d, 0.0d, 2.0d, -3.5d });
#line (73, 5) - (73, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Abs(a);
#line (74, 5) - (74, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 0.0d, 2.0d, 3.5d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestSinCosTanMatchMath()
            {
#line (78, 5) - (78, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 0.0d, math.Pi / 6, math.Pi / 4, math.Pi / 2 });
#line (79, 5) - (79, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var s = np.Sin(a);
#line (80, 5) - (80, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var c = np.Cos(a);
#line (81, 5) - (81, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var t = np.Tan(a);
#line (82, 5) - (82, 145) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(s, np.Array(new Sharpy.List<double>() { math.Sin(0.0d), math.Sin(math.Pi / 6), math.Sin(math.Pi / 4), math.Sin(math.Pi / 2) }), rtol: 0.0d, atol: 1e-12d));
#line (83, 5) - (83, 145) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(c, np.Array(new Sharpy.List<double>() { math.Cos(0.0d), math.Cos(math.Pi / 6), math.Cos(math.Pi / 4), math.Cos(math.Pi / 2) }), rtol: 0.0d, atol: 1e-12d));
#line (84, 5) - (84, 114) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(np.Array(new Sharpy.List<double>() { 0.0d, 1.0d }), np.Array(new Sharpy.List<double>() { np.Tan(0.0d), np.Tan(math.Pi / 4) }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestArcsinArccosArctanRoundTrip()
            {
#line (88, 5) - (88, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var x = np.Array(new Sharpy.List<double>() { 0.0d, 0.5d, 0.75d, -0.5d });
#line (89, 5) - (89, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var asx = np.Arcsin(x);
#line (90, 5) - (90, 129) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(asx, np.Array(new Sharpy.List<double>() { math.Asin(0.0d), math.Asin(0.5d), math.Asin(0.75d), math.Asin(-0.5d) }), rtol: 0.0d, atol: 1e-12d));
#line (91, 5) - (91, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var acx = np.Arccos(x);
#line (92, 5) - (92, 129) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(acx, np.Array(new Sharpy.List<double>() { math.Acos(0.0d), math.Acos(0.5d), math.Acos(0.75d), math.Acos(-0.5d) }), rtol: 0.0d, atol: 1e-12d));
#line (93, 5) - (93, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var atx = np.Arctan(x);
#line (94, 5) - (94, 129) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(atx, np.Array(new Sharpy.List<double>() { math.Atan(0.0d), math.Atan(0.5d), math.Atan(0.75d), math.Atan(-0.5d) }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestFloorCeilRoundMatchMath()
            {
#line (98, 5) - (98, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.2d, 1.5d, 1.7d, -1.2d, -1.5d, -1.7d });
#line (99, 5) - (99, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var fl = np.Floor(a);
#line (100, 5) - (100, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var ce = np.Ceil(a);
#line (101, 5) - (101, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var rd = np.Round(a);
#line (102, 5) - (102, 95) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(fl, np.Array(new Sharpy.List<double>() { 1.0d, 1.0d, 1.0d, -2.0d, -2.0d, -2.0d }), rtol: 0.0d, atol: 1e-12d));
#line (103, 5) - (103, 95) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(ce, np.Array(new Sharpy.List<double>() { 2.0d, 2.0d, 2.0d, -1.0d, -1.0d, -1.0d }), rtol: 0.0d, atol: 1e-12d));
#line (105, 5) - (105, 95) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(rd, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 2.0d, -1.0d, -2.0d, -2.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestRoundWithDecimalsHonorsParameter()
            {
#line (109, 5) - (109, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.2345d, 2.5555d, -3.1415d });
#line (110, 5) - (110, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Round(a, 2);
#line (111, 5) - (111, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.23d, 2.56d, -3.14d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestPowerArrayArrayBroadcastsAndComputes()
            {
#line (117, 5) - (117, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 2.0d, 3.0d, 4.0d });
#line (118, 5) - (118, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 2.0d, 2.0d, 0.5d });
#line (119, 5) - (119, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Power(a, b);
#line (120, 5) - (120, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 4.0d, 9.0d, 2.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestSumFullReductionAddsAllElements()
            {
#line (126, 5) - (126, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (127, 5) - (127, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(15.0d, np.Sum(a));
            }

            [Xunit.FactAttribute]
            public void TestSumEmptyArrayIsZero()
            {
#line (131, 5) - (131, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Zeros(0);
#line (132, 5) - (132, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(0.0d, np.Sum(a));
            }

            [Xunit.FactAttribute]
            public void TestMinFullReductionFindsMinimum()
            {
#line (136, 5) - (136, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 3.0d, 1.0d, 4.0d, 1.0d, 5.0d, 9.0d, 2.0d, 6.0d });
#line (137, 5) - (137, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(1.0d, np.Min(a));
            }

            [Xunit.FactAttribute]
            public void TestMaxFullReductionFindsMaximum()
            {
#line (141, 5) - (141, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 3.0d, 1.0d, 4.0d, 1.0d, 5.0d, 9.0d, 2.0d, 6.0d });
#line (142, 5) - (142, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(9.0d, np.Max(a));
            }

            [Xunit.FactAttribute]
            public void TestMeanFullReductionAveragesAllElements()
            {
#line (146, 5) - (146, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (147, 5) - (147, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(3.0d, np.Mean(a));
            }

            [Xunit.FactAttribute]
            public void TestVarFullReductionPopulationVariance()
            {
#line (151, 5) - (151, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (152, 5) - (152, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(2.0d, np.Var(a));
            }

            [Xunit.FactAttribute]
            public void TestStdFullReductionSqrtOfVariance()
            {
#line (156, 5) - (156, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (157, 5) - (157, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(math.Sqrt(2.0d), np.Std(a));
            }

            [Xunit.FactAttribute]
            public void TestMedianOddCountReturnsMiddleElement()
            {
#line (161, 5) - (161, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 5.0d, 1.0d, 3.0d, 2.0d, 4.0d });
#line (162, 5) - (162, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(3.0d, np.Median(a));
            }

            [Xunit.FactAttribute]
            public void TestMedianEvenCountAveragesTwoMiddleElements()
            {
#line (166, 5) - (166, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (167, 5) - (167, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(2.5d, np.Median(a));
            }

            [Xunit.FactAttribute]
            public void TestMinEmptyArrayThrows()
            {
#line (171, 5) - (171, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Zeros(0);
#line (172, 5) - (177, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Throws<InvalidOperationException>((global::System.Action)(() =>
                {
#line (173, 9) - (173, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                    np.Min(a);
                }));
            }

            [Xunit.FactAttribute]
            public void TestSumAlongAxis0CollapsesRows()
            {
#line (180, 5) - (180, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Arange(1.0d, 7.0d).Reshape(2, 3);
#line (181, 5) - (181, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Sum(a, 0);
#line (182, 5) - (182, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (183, 5) - (183, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 5.0d, 7.0d, 9.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestSumAlongAxis1CollapsesColumns()
            {
#line (188, 5) - (188, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Arange(1.0d, 7.0d).Reshape(2, 3);
#line (189, 5) - (189, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Sum(a, 1);
#line (190, 5) - (190, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (191, 5) - (191, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 6.0d, 15.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestMeanAlongAxisComputesPerRowOrColumn()
            {
#line (195, 5) - (195, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Arange(1.0d, 7.0d).Reshape(2, 3);
#line (196, 5) - (196, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r0 = np.Mean(a, 0);
#line (197, 5) - (197, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r1 = np.Mean(a, 1);
#line (198, 5) - (198, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r0.Shape, 0));
#line (199, 5) - (199, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r0, np.Array(new Sharpy.List<double>() { 2.5d, 3.5d, 4.5d }), rtol: 0.0d, atol: 1e-12d));
#line (200, 5) - (200, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r1.Shape, 0));
#line (201, 5) - (201, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r1, np.Array(new Sharpy.List<double>() { 2.0d, 5.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestMaxAlongAxisFindsPerSliceMaximum()
            {
#line (206, 5) - (206, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 9.0d, 3.0d, 4.0d, 2.0d, 6.0d }).Reshape(2, 3);
#line (207, 5) - (207, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Max(a, 1);
#line (208, 5) - (208, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 9.0d, 6.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestMedianAlongAxisPerRow()
            {
#line (213, 5) - (213, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 5.0d, 4.0d, 6.0d }).Reshape(2, 3);
#line (214, 5) - (214, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Median(a, 1);
#line (215, 5) - (215, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 2.0d, 5.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestSumAlongAxisNegativeAxisWrapsToTrailing()
            {
#line (220, 5) - (220, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Arange(1.0d, 7.0d).Reshape(2, 3);
#line (221, 5) - (221, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var r = np.Sum(a, -1);
#line (222, 5) - (222, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (223, 5) - (223, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 6.0d, 15.0d }), rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestSumAlongAxisOutOfRangeThrows()
            {
#line (227, 5) - (227, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                var a = np.Zeros(6).Reshape(2, 3);
#line (228, 5) - (230, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                Xunit.Assert.Throws<ArgumentOutOfRangeException>((global::System.Action)(() =>
                {
#line (229, 9) - (229, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                    np.Sum(a, 5);
                }));
            }
        }
    }
}
