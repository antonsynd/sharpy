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
using math = global::Sharpy.MathModule.MathModuleModule;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Numpy.NumpyMathTests
{
    [global::Sharpy.SharpyModule("numpy.numpy_math_tests")]
    public static partial class NumpyMathTestsModule
    {
    }

    public partial class NumpyMathTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestSqrtArray()
        {
#line (35, 5) - (35, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 4.0d, 9.0d, 16.0d });
#line (36, 5) - (36, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Sqrt(a);
#line (37, 5) - (37, 86) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d, 4.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSqrtScalar()
        {
#line (41, 5) - (41, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(5.0d, np.Sqrt(25.0d));
#line (42, 5) - (42, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(math.Sqrt(2.0d), np.Sqrt(2.0d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExpArray()
        {
#line (46, 5) - (46, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, -1.0d });
#line (47, 5) - (47, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Exp(a);
#line (48, 5) - (48, 87) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var expected = np.Array(new Sharpy.List<double>() { math.Exp(0.0d), math.Exp(1.0d), math.Exp(2.0d), math.Exp(-1.0d) });
#line (49, 5) - (49, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, expected, rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLogArray()
        {
#line (53, 5) - (53, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, global::Sharpy.MathModule.MathModuleModule.E, 10.0d });
#line (54, 5) - (54, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Log(a);
#line (55, 5) - (55, 87) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, math.Log(10.0d) }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLog2Array()
        {
#line (59, 5) - (59, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 4.0d, 8.0d });
#line (60, 5) - (60, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Log2(a);
#line (61, 5) - (61, 81) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLog10Array()
        {
#line (65, 5) - (65, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 10.0d, 100.0d, 1000.0d });
#line (66, 5) - (66, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Log10(a);
#line (67, 5) - (67, 81) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAbsArray()
        {
#line (71, 5) - (71, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { -1.0d, 0.0d, 2.0d, -3.5d });
#line (72, 5) - (72, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Abs(a);
#line (73, 5) - (73, 81) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.0d, 0.0d, 2.0d, 3.5d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSinCosTanMatchMath()
        {
#line (77, 5) - (77, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 0.0d, global::Sharpy.MathModule.MathModuleModule.Pi / 6, global::Sharpy.MathModule.MathModuleModule.Pi / 4, global::Sharpy.MathModule.MathModuleModule.Pi / 2 });
#line (78, 5) - (78, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var s = np.Sin(a);
#line (79, 5) - (79, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var c = np.Cos(a);
#line (80, 5) - (80, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var t = np.Tan(a);
#line (81, 5) - (81, 145) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(s, np.Array(new Sharpy.List<double>() { math.Sin(0.0d), math.Sin(global::Sharpy.MathModule.MathModuleModule.Pi / 6), math.Sin(global::Sharpy.MathModule.MathModuleModule.Pi / 4), math.Sin(global::Sharpy.MathModule.MathModuleModule.Pi / 2) }), rtol: 0.0d, atol: 1e-12d));
#line (82, 5) - (82, 145) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(c, np.Array(new Sharpy.List<double>() { math.Cos(0.0d), math.Cos(global::Sharpy.MathModule.MathModuleModule.Pi / 6), math.Cos(global::Sharpy.MathModule.MathModuleModule.Pi / 4), math.Cos(global::Sharpy.MathModule.MathModuleModule.Pi / 2) }), rtol: 0.0d, atol: 1e-12d));
#line (83, 5) - (83, 114) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(np.Array(new Sharpy.List<double>() { 0.0d, 1.0d }), np.Array(new Sharpy.List<double>() { np.Tan(0.0d), np.Tan(global::Sharpy.MathModule.MathModuleModule.Pi / 4) }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArcsinArccosArctanRoundTrip()
        {
#line (87, 5) - (87, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var x = np.Array(new Sharpy.List<double>() { 0.0d, 0.5d, 0.75d, -0.5d });
#line (88, 5) - (88, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var asx = np.Arcsin(x);
#line (89, 5) - (89, 129) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(asx, np.Array(new Sharpy.List<double>() { math.Asin(0.0d), math.Asin(0.5d), math.Asin(0.75d), math.Asin(-0.5d) }), rtol: 0.0d, atol: 1e-12d));
#line (90, 5) - (90, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var acx = np.Arccos(x);
#line (91, 5) - (91, 129) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(acx, np.Array(new Sharpy.List<double>() { math.Acos(0.0d), math.Acos(0.5d), math.Acos(0.75d), math.Acos(-0.5d) }), rtol: 0.0d, atol: 1e-12d));
#line (92, 5) - (92, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var atx = np.Arctan(x);
#line (93, 5) - (93, 129) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(atx, np.Array(new Sharpy.List<double>() { math.Atan(0.0d), math.Atan(0.5d), math.Atan(0.75d), math.Atan(-0.5d) }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestFloorCeilRoundMatchMath()
        {
#line (97, 5) - (97, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.2d, 1.5d, 1.7d, -1.2d, -1.5d, -1.7d });
#line (98, 5) - (98, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var fl = np.Floor(a);
#line (99, 5) - (99, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var ce = np.Ceil(a);
#line (100, 5) - (100, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var rd = np.Round(a);
#line (101, 5) - (101, 95) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(fl, np.Array(new Sharpy.List<double>() { 1.0d, 1.0d, 1.0d, -2.0d, -2.0d, -2.0d }), rtol: 0.0d, atol: 1e-12d));
#line (102, 5) - (102, 95) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(ce, np.Array(new Sharpy.List<double>() { 2.0d, 2.0d, 2.0d, -1.0d, -1.0d, -1.0d }), rtol: 0.0d, atol: 1e-12d));
#line (104, 5) - (104, 95) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(rd, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 2.0d, -1.0d, -2.0d, -2.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRoundWithDecimalsHonorsParameter()
        {
#line (108, 5) - (108, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.2345d, 2.5555d, -3.1415d });
#line (109, 5) - (109, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Round(a, 2);
#line (110, 5) - (110, 80) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 1.23d, 2.56d, -3.14d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestPowerArrayArrayBroadcastsAndComputes()
        {
#line (116, 5) - (116, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 2.0d, 3.0d, 4.0d });
#line (117, 5) - (117, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var b = np.Array(new Sharpy.List<double>() { 2.0d, 2.0d, 0.5d });
#line (118, 5) - (118, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Power(a, b);
#line (119, 5) - (119, 76) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 4.0d, 9.0d, 2.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSumFullReductionAddsAllElements()
        {
#line (125, 5) - (125, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (126, 5) - (126, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(15.0d, np.Sum(a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSumEmptyArrayIsZero()
        {
#line (130, 5) - (130, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Zeros(0);
#line (131, 5) - (131, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(0.0d, np.Sum(a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMinFullReductionFindsMinimum()
        {
#line (135, 5) - (135, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 3.0d, 1.0d, 4.0d, 1.0d, 5.0d, 9.0d, 2.0d, 6.0d });
#line (136, 5) - (136, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(1.0d, np.Min(a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMaxFullReductionFindsMaximum()
        {
#line (140, 5) - (140, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 3.0d, 1.0d, 4.0d, 1.0d, 5.0d, 9.0d, 2.0d, 6.0d });
#line (141, 5) - (141, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(9.0d, np.Max(a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMeanFullReductionAveragesAllElements()
        {
#line (145, 5) - (145, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (146, 5) - (146, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(3.0d, np.Mean(a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVarFullReductionPopulationVariance()
        {
#line (150, 5) - (150, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (151, 5) - (151, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(2.0d, np.Var(a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStdFullReductionSqrtOfVariance()
        {
#line (155, 5) - (155, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (156, 5) - (156, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(math.Sqrt(2.0d), np.Std(a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMedianOddCountReturnsMiddleElement()
        {
#line (160, 5) - (160, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 5.0d, 1.0d, 3.0d, 2.0d, 4.0d });
#line (161, 5) - (161, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(3.0d, np.Median(a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMedianEvenCountAveragesTwoMiddleElements()
        {
#line (165, 5) - (165, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d });
#line (166, 5) - (166, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(2.5d, np.Median(a));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMinEmptyArrayThrows()
        {
#line (170, 5) - (170, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Zeros(0);
#line (171, 5) - (172, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (172, 9) - (172, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                np.Min(a);
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
        public void TestSumAlongAxis0CollapsesRows()
        {
#line (179, 5) - (179, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Arange(1.0d, 7.0d).Reshape(2, 3);
#line (180, 5) - (180, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Sum(a, 0);
#line (181, 5) - (181, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (182, 5) - (182, 76) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 5.0d, 7.0d, 9.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSumAlongAxis1CollapsesColumns()
        {
#line (187, 5) - (187, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Arange(1.0d, 7.0d).Reshape(2, 3);
#line (188, 5) - (188, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Sum(a, 1);
#line (189, 5) - (189, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (190, 5) - (190, 72) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 6.0d, 15.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMeanAlongAxisComputesPerRowOrColumn()
        {
#line (194, 5) - (194, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Arange(1.0d, 7.0d).Reshape(2, 3);
#line (195, 5) - (195, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r0 = np.Mean(a, 0);
#line (196, 5) - (196, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r1 = np.Mean(a, 1);
#line (197, 5) - (197, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(r0.Shape, 0));
#line (198, 5) - (198, 77) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r0, np.Array(new Sharpy.List<double>() { 2.5d, 3.5d, 4.5d }), rtol: 0.0d, atol: 1e-12d));
#line (199, 5) - (199, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r1.Shape, 0));
#line (200, 5) - (200, 72) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r1, np.Array(new Sharpy.List<double>() { 2.0d, 5.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMaxAlongAxisFindsPerSliceMaximum()
        {
#line (205, 5) - (205, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 9.0d, 3.0d, 4.0d, 2.0d, 6.0d }).Reshape(2, 3);
#line (206, 5) - (206, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Max(a, 1);
#line (207, 5) - (207, 71) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 9.0d, 6.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMedianAlongAxisPerRow()
        {
#line (212, 5) - (212, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 5.0d, 4.0d, 6.0d }).Reshape(2, 3);
#line (213, 5) - (213, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Median(a, 1);
#line (214, 5) - (214, 71) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 2.0d, 5.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSumAlongAxisNegativeAxisWrapsToTrailing()
        {
#line (219, 5) - (219, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Arange(1.0d, 7.0d).Reshape(2, 3);
#line (220, 5) - (220, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var r = np.Sum(a, -1);
#line (221, 5) - (221, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(r.Shape, 0));
#line (222, 5) - (222, 72) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            Xunit.Assert.True(np.Allclose(r, np.Array(new Sharpy.List<double>() { 6.0d, 15.0d }), rtol: 0.0d, atol: 1e-12d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSumAlongAxisOutOfRangeThrows()
        {
#line (226, 5) - (226, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            var a = np.Zeros(6).Reshape(2, 3);
#line (227, 5) - (228, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (228, 9) - (228, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_math_tests.spy"
                np.Sum(a, 5);
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
