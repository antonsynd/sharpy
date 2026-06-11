// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using math = global::Sharpy.MathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Math.MathAdditional2Tests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Math
    {
        [global::Sharpy.SharpyModule("math.math_additional2_tests")]
        public static partial class MathAdditional2Tests
        {
        }
    }

    public static partial class Math
    {
        public partial class MathAdditional2TestsTests
        {
            [Xunit.FactAttribute]
            public void TestSinZeroReturnsZero()
            {
#line (9, 5) - (9, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Sin(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestSinPiOver2ReturnsOne()
            {
#line (13, 5) - (13, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(1.0d, math.Sin(math.Pi / 2.0d), absTol: 1e-15d));
            }

            [Xunit.FactAttribute]
            public void TestSinPiIsApproximatelyZero()
            {
#line (17, 5) - (17, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(0.0d, math.Sin(math.Pi), absTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestCosZeroReturnsOne()
            {
#line (21, 5) - (21, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(1.0d, math.Cos(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestCosPiReturnsNegativeOne()
            {
#line (25, 5) - (25, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(-1.0d, math.Cos(math.Pi), absTol: 1e-15d));
            }

            [Xunit.FactAttribute]
            public void TestTanZeroReturnsZero()
            {
#line (29, 5) - (29, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Tan(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestTanPiOver4IsApproximatelyOne()
            {
#line (33, 5) - (33, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(1.0d, math.Tan(math.Pi / 4.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestAsinOneReturnsPiOver2()
            {
#line (39, 5) - (39, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(math.Pi / 2.0d, math.Asin(1.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestAsinZeroReturnsZero()
            {
#line (43, 5) - (43, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Asin(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestAcosOneReturnsZero()
            {
#line (47, 5) - (47, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Acos(1.0d));
            }

            [Xunit.FactAttribute]
            public void TestAcosZeroReturnsPiOver2()
            {
#line (51, 5) - (51, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(math.Pi / 2.0d, math.Acos(0.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestAtanZeroReturnsZero()
            {
#line (55, 5) - (55, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Atan(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestAtanOneReturnsPiOver4()
            {
#line (59, 5) - (59, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(math.Pi / 4.0d, math.Atan(1.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestAtan2OneOneReturnsPiOver4()
            {
#line (63, 5) - (63, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(math.Pi / 4.0d, math.Atan2(1.0d, 1.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestAtan2NegativeXReturnsPi()
            {
#line (67, 5) - (67, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(math.Pi, math.Atan2(0.0d, -1.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestSinhZeroReturnsZero()
            {
#line (73, 5) - (73, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Sinh(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestSinhOneMatchesKnownValue()
            {
#line (77, 5) - (77, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(1.1752011936438014d, math.Sinh(1.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestCoshZeroReturnsOne()
            {
#line (81, 5) - (81, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(1.0d, math.Cosh(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestTanhZeroReturnsZero()
            {
#line (85, 5) - (85, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Tanh(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestExpZeroReturnsOne()
            {
#line (91, 5) - (91, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(1.0d, math.Exp(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestExpOneReturnsE()
            {
#line (95, 5) - (95, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(math.E, math.Exp(1.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestLogOneReturnsZero()
            {
#line (101, 5) - (101, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Log(1.0d));
            }

            [Xunit.FactAttribute]
            public void TestLogEReturnsOne()
            {
#line (105, 5) - (105, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(1.0d, math.Log(math.E), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestLogWithBase10ReturnsCorrectValue()
            {
#line (109, 5) - (109, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(2.0d, math.Log(100.0d, 10.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestLogWithBase1IsSpecialCase()
            {
#line (113, 5) - (113, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(0.0d, math.Log(1.0d, 10.0d), absTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestLog10HundredReturnsTwo()
            {
#line (117, 5) - (117, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(2.0d, math.Log10(100.0d));
            }

            [Xunit.FactAttribute]
            public void TestLog10OneReturnsZero()
            {
#line (121, 5) - (121, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Log10(1.0d));
            }

            [Xunit.FactAttribute]
            public void TestLog2EightReturnsThree()
            {
#line (125, 5) - (125, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(3.0d, math.Log2(8.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestLog2OneReturnsZero()
            {
#line (129, 5) - (129, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Log2(1.0d));
            }

            [Xunit.FactAttribute]
            public void TestSqrtFourReturnsTwo()
            {
#line (135, 5) - (135, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(2.0d, math.Sqrt(4.0d));
            }

            [Xunit.FactAttribute]
            public void TestSqrtZeroReturnsZero()
            {
#line (139, 5) - (139, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Sqrt(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestSqrtOneReturnsOne()
            {
#line (143, 5) - (143, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(1.0d, math.Sqrt(1.0d));
            }

            [Xunit.FactAttribute]
            public void TestSqrtNegativeNumberReturnsNan()
            {
#line (147, 5) - (147, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isnan(math.Sqrt(-1.0d)));
            }

            [Xunit.FactAttribute]
            public void TestPowTwoToTenReturns1024()
            {
#line (151, 5) - (151, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(1024.0d, math.Pow(2.0d, 10.0d));
            }

            [Xunit.FactAttribute]
            public void TestPowAnyNumberToZeroReturnsOne()
            {
#line (155, 5) - (155, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(1.0d, math.Pow(5.0d, 0.0d));
            }

            [Xunit.FactAttribute]
            public void TestPowZeroToPositiveReturnsZero()
            {
#line (159, 5) - (159, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Pow(0.0d, 3.0d));
            }

            [Xunit.FactAttribute]
            public void TestCeilPositiveFractionalRoundsUp()
            {
#line (165, 5) - (165, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(4.0d, math.Ceil(3.2d));
            }

            [Xunit.FactAttribute]
            public void TestCeilNegativeFractionalRoundsTowardZero()
            {
#line (169, 5) - (169, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Ceil(-0.5d));
            }

            [Xunit.FactAttribute]
            public void TestFloorPositiveFractionalRoundsDown()
            {
#line (173, 5) - (173, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(3.0d, math.Floor(3.7d));
            }

            [Xunit.FactAttribute]
            public void TestFloorNegativeFractionalRoundsAwayFromZero()
            {
#line (177, 5) - (177, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(-1.0d, math.Floor(-0.5d));
            }

            [Xunit.FactAttribute]
            public void TestTruncPositiveFractionalTruncatesTowardZero()
            {
#line (181, 5) - (181, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(3.0d, math.Trunc(3.7d));
            }

            [Xunit.FactAttribute]
            public void TestTruncNegativeFractionalTruncatesTowardZero()
            {
#line (185, 5) - (185, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(-3.0d, math.Trunc(-3.7d));
            }

            [Xunit.FactAttribute]
            public void TestFabsNegativeValueReturnsPositive()
            {
#line (189, 5) - (189, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(3.7d, math.Fabs(-3.7d));
            }

            [Xunit.FactAttribute]
            public void TestFabsPositiveValueReturnsSame()
            {
#line (193, 5) - (193, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(3.7d, math.Fabs(3.7d));
            }

            [Xunit.FactAttribute]
            public void TestFabsZeroReturnsZero()
            {
#line (197, 5) - (197, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Fabs(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestDegreesPiReturns180()
            {
#line (203, 5) - (203, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(180.0d, math.Degrees(math.Pi), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestDegreesZeroReturnsZero()
            {
#line (207, 5) - (207, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Degrees(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestRadians180ReturnsPi()
            {
#line (211, 5) - (211, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(math.Pi, math.Radians(180.0d), relTol: 1e-14d));
            }

            [Xunit.FactAttribute]
            public void TestRadiansZeroReturnsZero()
            {
#line (215, 5) - (215, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Radians(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestIsfiniteNormalNumberReturnsTrue()
            {
#line (221, 5) - (221, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isfinite(1.0d));
            }

            [Xunit.FactAttribute]
            public void TestIsfinitePositiveInfinityReturnsFalse()
            {
#line (225, 5) - (225, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.False(math.Isfinite(math.Inf));
            }

            [Xunit.FactAttribute]
            public void TestIsfiniteNanReturnsFalse()
            {
#line (229, 5) - (229, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.False(math.Isfinite(math.Nan));
            }

            [Xunit.FactAttribute]
            public void TestIsinfPositiveInfinityReturnsTrue()
            {
#line (233, 5) - (233, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isinf(math.Inf));
            }

            [Xunit.FactAttribute]
            public void TestIsinfNegativeInfinityReturnsTrue()
            {
#line (237, 5) - (237, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isinf(-math.Inf));
            }

            [Xunit.FactAttribute]
            public void TestIsinfNormalNumberReturnsFalse()
            {
#line (241, 5) - (241, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.False(math.Isinf(42.0d));
            }

            [Xunit.FactAttribute]
            public void TestIsnanNanReturnsTrue()
            {
#line (245, 5) - (245, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isnan(math.Nan));
            }

            [Xunit.FactAttribute]
            public void TestIsnanNormalNumberReturnsFalse()
            {
#line (249, 5) - (249, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.False(math.Isnan(1.0d));
            }

            [Xunit.FactAttribute]
            public void TestIsnanInfinityReturnsFalse()
            {
#line (253, 5) - (253, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.False(math.Isnan(math.Inf));
            }

            [Xunit.FactAttribute]
            public void TestCopysignPositiveMagnitudeNegativeSignReturnsNegative()
            {
#line (259, 5) - (259, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(-1.0d, math.Copysign(1.0d, -2.0d));
            }

            [Xunit.FactAttribute]
            public void TestCopysignPositiveMagnitudePositiveSignReturnsPositive()
            {
#line (263, 5) - (263, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(5.0d, math.Copysign(5.0d, 3.0d));
            }

            [Xunit.FactAttribute]
            public void TestCopysignNegativeMagnitudePositiveSignReturnsPositive()
            {
#line (267, 5) - (267, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(3.0d, math.Copysign(-3.0d, 1.0d));
            }

            [Xunit.FactAttribute]
            public void TestGcdTwelveAndEightReturnsFour()
            {
#line (273, 5) - (273, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(4, math.Gcd(12, 8));
            }

            [Xunit.FactAttribute]
            public void TestGcdCoprimePairReturnsOne()
            {
#line (277, 5) - (277, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(1, math.Gcd(7, 11));
            }

            [Xunit.FactAttribute]
            public void TestGcdZeroAndNReturnsN()
            {
#line (281, 5) - (281, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(5, math.Gcd(0, 5));
            }

            [Xunit.FactAttribute]
            public void TestGcdNegativeValuesReturnsPositive()
            {
#line (285, 5) - (285, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(4, math.Gcd(-12, 8));
            }

            [Xunit.FactAttribute]
            public void TestFactorialZeroReturnsOne()
            {
#line (291, 5) - (291, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(1, math.Factorial(0));
            }

            [Xunit.FactAttribute]
            public void TestFactorialFiveReturns120()
            {
#line (295, 5) - (295, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(120, math.Factorial(5));
            }

            [Xunit.FactAttribute]
            public void TestFactorialNegativeThrowsValueError()
            {
#line (299, 5) - (302, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (300, 9) - (300, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                    math.Factorial(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestFactorialTooLargeThrowsOverflowError()
            {
#line (304, 5) - (309, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Throws<OverflowError>((global::System.Action)(() =>
                {
#line (305, 9) - (305, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                    math.Factorial(21);
                }));
            }

            [Xunit.FactAttribute]
            public void TestLogZeroReturnsNegativeInfinity()
            {
#line (311, 5) - (311, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(-math.Inf, math.Log(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestFsumNegativeValuesSumsCorrectly()
            {
#line (317, 5) - (317, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.Equal(-6.0d, math.Fsum(new Sharpy.List<double>() { -1.0d, -2.0d, -3.0d }));
            }

            [Xunit.FactAttribute]
            public void TestIscloseFloatingPointApproximationReturnsTrue()
            {
#line (323, 5) - (323, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional2_tests.spy"
                Xunit.Assert.True(math.Isclose(0.1d + 0.2d, 0.3d));
            }
        }
    }
}
