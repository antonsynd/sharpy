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
using static Sharpy.Stdlib.Tests.Spy.Math.MathAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Math
    {
        [global::Sharpy.SharpyModule("math.math_additional_tests")]
        public static partial class MathAdditionalTests
        {
        }
    }

    public static partial class Math
    {
        public partial class MathAdditionalTestsTests
        {
            [Xunit.FactAttribute]
            public void TestLcmBasicValues()
            {
#line (9, 5) - (9, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(12, math.Lcm(4, 6));
#line (10, 5) - (10, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(12, math.Lcm(6, 4));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLcmCoprimePair()
            {
#line (14, 5) - (14, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(35, math.Lcm(5, 7));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLcmOneIsMultipleOfOther()
            {
#line (18, 5) - (18, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(12, math.Lcm(4, 12));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLcmWithZero()
            {
#line (22, 5) - (22, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0, math.Lcm(0, 5));
#line (23, 5) - (23, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0, math.Lcm(5, 0));
#line (24, 5) - (24, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0, math.Lcm(0, 0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLcmNegativeValues()
            {
#line (28, 5) - (28, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(12, math.Lcm(-4, 6));
#line (29, 5) - (29, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(12, math.Lcm(4, -6));
#line (30, 5) - (30, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(12, math.Lcm(-4, -6));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLcmSameValues()
            {
#line (34, 5) - (34, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(7, math.Lcm(7, 7));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIscloseEqualValues()
            {
#line (40, 5) - (40, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(1.0d, 1.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIscloseCloseValues()
            {
#line (44, 5) - (44, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(1.0d, 1.0d + 1e-10d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIscloseNotCloseValues()
            {
#line (48, 5) - (48, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.False(math.Isclose(1.0d, 2.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIscloseWithAbsTol()
            {
#line (52, 5) - (52, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(0.0d, 0.001d, absTol: 0.01d));
#line (53, 5) - (53, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.False(math.Isclose(0.0d, 0.001d, absTol: 0.0001d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIscloseWithRelTol()
            {
#line (57, 5) - (57, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(100.0d, 100.1d, relTol: 0.01d));
#line (58, 5) - (58, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.False(math.Isclose(100.0d, 102.0d, relTol: 0.01d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIscloseInfinity()
            {
#line (62, 5) - (62, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(math.Inf, math.Inf));
#line (63, 5) - (63, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.False(math.Isclose(math.Inf, 1.0d));
#line (64, 5) - (64, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.False(math.Isclose(1.0d, -math.Inf));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIscloseNan()
            {
#line (68, 5) - (68, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.False(math.Isclose(math.Nan, math.Nan));
#line (69, 5) - (69, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.False(math.Isclose(math.Nan, 1.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIscloseNegativeToleranceThrows()
            {
#line (73, 5) - (75, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (74, 9) - (74, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                    math.Isclose(1.0d, 1.0d, relTol: -1.0d);
#line hidden
                }));
#line (75, 5) - (80, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (76, 9) - (76, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                    math.Isclose(1.0d, 1.0d, absTol: -1.0d);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestCombBasicValues()
            {
#line (82, 5) - (82, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(10, math.Comb(5, 2));
#line (83, 5) - (83, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(1, math.Comb(5, 0));
#line (84, 5) - (84, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(1, math.Comb(5, 5));
#line (85, 5) - (85, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(5, math.Comb(5, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCombKGreaterThanN()
            {
#line (89, 5) - (89, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0, math.Comb(3, 5));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCombNegativeNThrows()
            {
#line (93, 5) - (96, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (94, 9) - (94, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                    math.Comb(-1, 2);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestCombNegativeKThrows()
            {
#line (98, 5) - (101, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (99, 9) - (99, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                    math.Comb(5, -1);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestCombLargerValues()
            {
#line (103, 5) - (103, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(120, math.Comb(10, 3));
#line (104, 5) - (104, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(184756, math.Comb(20, 10));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPermBasicValues()
            {
#line (110, 5) - (110, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(20, math.Perm(5, 2));
#line (111, 5) - (111, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(1, math.Perm(5, 0));
#line (112, 5) - (112, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(120, math.Perm(5, 5));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPermKGreaterThanN()
            {
#line (116, 5) - (116, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0, math.Perm(3, 5));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPermNegativeNThrows()
            {
#line (120, 5) - (123, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (121, 9) - (121, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                    math.Perm(-1, 2);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestPermNegativeKThrows()
            {
#line (125, 5) - (128, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (126, 9) - (126, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                    math.Perm(5, -1);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestPermSingleArgReturnsFactorial()
            {
#line (130, 5) - (130, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(120, math.Perm(5));
#line (131, 5) - (131, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(1, math.Perm(0));
#line (132, 5) - (132, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(1, math.Perm(1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFsumBasicValues()
            {
#line (138, 5) - (138, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(6.0d, math.Fsum(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d }));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFsumAccuracyTest()
            {
#line (142, 5) - (142, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Sharpy.List<double> values = new Sharpy.List<double>()
#line hidden
                {
                    0.1d,
                    0.1d,
                    0.1d,
                    0.1d,
                    0.1d,
                    0.1d,
                    0.1d,
                    0.1d,
                    0.1d,
                    0.1d
                };
#line (143, 5) - (143, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(1.0d, math.Fsum(values));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFsumEmptyIterable()
            {
#line (147, 5) - (147, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Sharpy.List<double> empty = new Sharpy.List<double>()
#line hidden
                {
                };
#line (148, 5) - (148, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Fsum(empty));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFsumSingleValue()
            {
#line (152, 5) - (152, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(42.5d, math.Fsum(new Sharpy.List<double>() { 42.5d }));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestProdDoubleValues()
            {
#line (158, 5) - (158, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(24.0d, math.Prod(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestProdIntValues()
            {
#line (162, 5) - (162, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(24, math.Prod(new Sharpy.List<int>() { 1, 2, 3, 4 }));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestProdWithStart()
            {
#line (166, 5) - (166, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(48.0d, math.Prod(new Sharpy.List<double>() { 2.0d, 3.0d, 4.0d }, start: 2.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestProdEmptyIterable()
            {
#line (170, 5) - (170, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Sharpy.List<double> emptyF = new Sharpy.List<double>()
#line hidden
                {
                };
#line (171, 5) - (171, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Sharpy.List<int> emptyI = new Sharpy.List<int>()
#line hidden
                {
                };
#line (172, 5) - (172, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(1.0d, math.Prod(emptyF));
#line (173, 5) - (173, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(1, math.Prod(emptyI));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestProdWithZero()
            {
#line (177, 5) - (177, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Prod(new Sharpy.List<double>() { 1.0d, 0.0d, 3.0d }));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHypotThreeFourFive()
            {
#line (183, 5) - (183, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(5.0d, math.Hypot(3.0d, 4.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHypotZeroValues()
            {
#line (187, 5) - (187, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Hypot(0.0d, 0.0d));
#line (188, 5) - (188, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(3.0d, math.Hypot(3.0d, 0.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHypotNegativeValues()
            {
#line (192, 5) - (192, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(5.0d, math.Hypot(-3.0d, 4.0d));
#line (193, 5) - (193, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(5.0d, math.Hypot(3.0d, -4.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExpm1Zero()
            {
#line (199, 5) - (199, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(0.0d, math.Expm1(0.0d), absTol: 1e-15d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExpm1SmallValue()
            {
#line (203, 5) - (203, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                double result = math.Expm1(1e-10d);
#line (204, 5) - (204, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(1e-10d, result, relTol: 1e-6d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExpm1LargerValue()
            {
#line (208, 5) - (208, 69) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(math.E - 1.0d, math.Expm1(1.0d), relTol: 1e-9d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLog1pZero()
            {
#line (214, 5) - (214, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Log1p(0.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLog1pSmallValue()
            {
#line (218, 5) - (218, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                double result = math.Log1p(1e-10d);
#line (219, 5) - (219, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(1e-10d, result, relTol: 1e-6d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLog1pLargerValue()
            {
#line (223, 5) - (223, 69) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.True(math.Isclose(1.0d, math.Log1p(math.E - 1.0d), relTol: 1e-9d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLog1pNegativeOneThrows()
            {
#line (227, 5) - (230, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (228, 9) - (228, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                    math.Log1p(-1.0d);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLog1pBelowNegativeOneThrows()
            {
#line (232, 5) - (237, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (233, 9) - (233, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                    math.Log1p(-2.0d);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestRemainderBasicValues()
            {
#line (239, 5) - (239, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(1.0d, math.Remainder(10.0d, 3.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRemainderNegativeResult()
            {
#line (243, 5) - (243, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(-1.0d, math.Remainder(11.0d, 3.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRemainderZeroDivisorThrows()
            {
#line (247, 5) - (250, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (248, 9) - (248, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                    math.Remainder(10.0d, 0.0d);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestRemainderZeroDividend()
            {
#line (252, 5) - (252, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/math/math_additional_tests.spy"
                Xunit.Assert.Equal(0.0d, math.Remainder(0.0d, 3.0d));
#line hidden
            }
        }
    }
}
#line default
