using Xunit;

namespace Sharpy.Tests
{
    public class MathAdditionalTests2
    {
        // --- Trigonometric functions ---

        [Fact]
        public void Sin_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Sin(0.0));
        }

        [Fact]
        public void Sin_PiOver2_ReturnsOne()
        {
            // sin(π/2) = 1.0
            Assert.True(MathModule.Isclose(1.0, MathModule.Sin(MathModule.Pi / 2.0), absTol: 1e-15));
        }

        [Fact]
        public void Sin_Pi_IsApproximatelyZero()
        {
            // sin(π) ≈ 0 (floating-point rounding produces ~1.22e-16)
            Assert.True(MathModule.Isclose(0.0, MathModule.Sin(MathModule.Pi), absTol: 1e-14));
        }

        [Fact]
        public void Cos_Zero_ReturnsOne()
        {
            Assert.Equal(1.0, MathModule.Cos(0.0));
        }

        [Fact]
        public void Cos_Pi_ReturnsNegativeOne()
        {
            Assert.True(MathModule.Isclose(-1.0, MathModule.Cos(MathModule.Pi), absTol: 1e-15));
        }

        [Fact]
        public void Tan_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Tan(0.0));
        }

        [Fact]
        public void Tan_PiOver4_IsApproximatelyOne()
        {
            // tan(π/4) ≈ 1.0 (floating-point: 0.9999999999999999)
            Assert.True(MathModule.Isclose(1.0, MathModule.Tan(MathModule.Pi / 4.0), relTol: 1e-14));
        }

        // --- Inverse trigonometric functions ---

        [Fact]
        public void Asin_One_ReturnsPiOver2()
        {
            Assert.True(MathModule.Isclose(MathModule.Pi / 2.0, MathModule.Asin(1.0), relTol: 1e-14));
        }

        [Fact]
        public void Asin_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Asin(0.0));
        }

        [Fact]
        public void Acos_One_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Acos(1.0));
        }

        [Fact]
        public void Acos_Zero_ReturnsPiOver2()
        {
            Assert.True(MathModule.Isclose(MathModule.Pi / 2.0, MathModule.Acos(0.0), relTol: 1e-14));
        }

        [Fact]
        public void Atan_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Atan(0.0));
        }

        [Fact]
        public void Atan_One_ReturnsPiOver4()
        {
            Assert.True(MathModule.Isclose(MathModule.Pi / 4.0, MathModule.Atan(1.0), relTol: 1e-14));
        }

        [Fact]
        public void Atan2_OneOne_ReturnsPiOver4()
        {
            Assert.True(MathModule.Isclose(MathModule.Pi / 4.0, MathModule.Atan2(1.0, 1.0), relTol: 1e-14));
        }

        [Fact]
        public void Atan2_NegativeX_ReturnsPi()
        {
            // atan2(0, -1) = π
            Assert.True(MathModule.Isclose(MathModule.Pi, MathModule.Atan2(0.0, -1.0), relTol: 1e-14));
        }

        // --- Hyperbolic functions ---

        [Fact]
        public void Sinh_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Sinh(0.0));
        }

        [Fact]
        public void Sinh_One_MatchesKnownValue()
        {
            // sinh(1) ≈ 1.1752011936438014
            Assert.True(MathModule.Isclose(1.1752011936438014, MathModule.Sinh(1.0), relTol: 1e-14));
        }

        [Fact]
        public void Cosh_Zero_ReturnsOne()
        {
            Assert.Equal(1.0, MathModule.Cosh(0.0));
        }

        [Fact]
        public void Tanh_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Tanh(0.0));
        }

        // --- Exponential functions ---

        [Fact]
        public void Exp_Zero_ReturnsOne()
        {
            Assert.Equal(1.0, MathModule.Exp(0.0));
        }

        [Fact]
        public void Exp_One_ReturnsE()
        {
            Assert.True(MathModule.Isclose(MathModule.E, MathModule.Exp(1.0), relTol: 1e-14));
        }

        // --- Logarithmic functions ---

        [Fact]
        public void Log_One_ReturnsZero()
        {
            // ln(1) = 0
            Assert.Equal(0.0, MathModule.Log(1.0));
        }

        [Fact]
        public void Log_E_ReturnsOne()
        {
            // ln(e) = 1
            Assert.True(MathModule.Isclose(1.0, MathModule.Log(MathModule.E), relTol: 1e-14));
        }

        [Fact]
        public void Log_WithBase10_ReturnsCorrectValue()
        {
            // log(100, 10) = 2.0
            Assert.True(MathModule.Isclose(2.0, MathModule.Log(100.0, 10.0), relTol: 1e-14));
        }

        [Fact]
        public void Log_WithBase1_IsSpecialCase()
        {
            // log(1, 10) = 0.0
            Assert.True(MathModule.Isclose(0.0, MathModule.Log(1.0, 10.0), absTol: 1e-14));
        }

        [Fact]
        public void Log10_HundredReturnsTwo()
        {
            Assert.Equal(2.0, MathModule.Log10(100.0));
        }

        [Fact]
        public void Log10_OneReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Log10(1.0));
        }

        [Fact]
        public void Log2_EightReturnsThree()
        {
            Assert.True(MathModule.Isclose(3.0, MathModule.Log2(8.0), relTol: 1e-14));
        }

        [Fact]
        public void Log2_OneReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Log2(1.0));
        }

        // --- Power and square root ---

        [Fact]
        public void Sqrt_Four_ReturnsTwo()
        {
            Assert.Equal(2.0, MathModule.Sqrt(4.0));
        }

        [Fact]
        public void Sqrt_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Sqrt(0.0));
        }

        [Fact]
        public void Sqrt_One_ReturnsOne()
        {
            Assert.Equal(1.0, MathModule.Sqrt(1.0));
        }

        [Fact]
        public void Sqrt_NegativeNumber_ReturnsNaN()
        {
            // .NET returns NaN for sqrt(-1) (unlike Python which throws ValueError)
            Assert.True(double.IsNaN(MathModule.Sqrt(-1.0)));
        }

        [Fact]
        public void Pow_TwoToTen_Returns1024()
        {
            Assert.Equal(1024.0, MathModule.Pow(2.0, 10.0));
        }

        [Fact]
        public void Pow_AnyNumberToZero_ReturnsOne()
        {
            Assert.Equal(1.0, MathModule.Pow(5.0, 0.0));
        }

        [Fact]
        public void Pow_ZeroToPositive_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Pow(0.0, 3.0));
        }

        // --- Rounding functions ---

        [Fact]
        public void Ceil_PositiveFractional_RoundsUp()
        {
            Assert.Equal(4.0, MathModule.Ceil(3.2));
        }

        [Fact]
        public void Ceil_NegativeFractional_RoundsTowardZero()
        {
            // ceil(-0.5) = 0 (toward positive infinity)
            Assert.Equal(0.0, MathModule.Ceil(-0.5));
        }

        [Fact]
        public void Floor_PositiveFractional_RoundsDown()
        {
            Assert.Equal(3.0, MathModule.Floor(3.7));
        }

        [Fact]
        public void Floor_NegativeFractional_RoundsAwayFromZero()
        {
            // floor(-0.5) = -1 (toward negative infinity)
            Assert.Equal(-1.0, MathModule.Floor(-0.5));
        }

        [Fact]
        public void Trunc_PositiveFractional_TruncatesTowardZero()
        {
            Assert.Equal(3.0, MathModule.Trunc(3.7));
        }

        [Fact]
        public void Trunc_NegativeFractional_TruncatesTowardZero()
        {
            Assert.Equal(-3.0, MathModule.Trunc(-3.7));
        }

        [Fact]
        public void Fabs_NegativeValue_ReturnsPositive()
        {
            Assert.Equal(3.7, MathModule.Fabs(-3.7));
        }

        [Fact]
        public void Fabs_PositiveValue_ReturnsSame()
        {
            Assert.Equal(3.7, MathModule.Fabs(3.7));
        }

        [Fact]
        public void Fabs_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Fabs(0.0));
        }

        // --- Degrees and radians ---

        [Fact]
        public void Degrees_Pi_Returns180()
        {
            Assert.True(MathModule.Isclose(180.0, MathModule.Degrees(MathModule.Pi), relTol: 1e-14));
        }

        [Fact]
        public void Degrees_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Degrees(0.0));
        }

        [Fact]
        public void Radians_180_ReturnsPi()
        {
            Assert.True(MathModule.Isclose(MathModule.Pi, MathModule.Radians(180.0), relTol: 1e-14));
        }

        [Fact]
        public void Radians_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, MathModule.Radians(0.0));
        }

        // --- Special value checks ---

        [Fact]
        public void Isfinite_NormalNumber_ReturnsTrue()
        {
            Assert.True(MathModule.Isfinite(1.0));
        }

        [Fact]
        public void Isfinite_PositiveInfinity_ReturnsFalse()
        {
            Assert.False(MathModule.Isfinite(double.PositiveInfinity));
        }

        [Fact]
        public void Isfinite_NaN_ReturnsFalse()
        {
            Assert.False(MathModule.Isfinite(double.NaN));
        }

        [Fact]
        public void Isinf_PositiveInfinity_ReturnsTrue()
        {
            Assert.True(MathModule.Isinf(double.PositiveInfinity));
        }

        [Fact]
        public void Isinf_NegativeInfinity_ReturnsTrue()
        {
            Assert.True(MathModule.Isinf(double.NegativeInfinity));
        }

        [Fact]
        public void Isinf_NormalNumber_ReturnsFalse()
        {
            Assert.False(MathModule.Isinf(42.0));
        }

        [Fact]
        public void Isnan_NaN_ReturnsTrue()
        {
            Assert.True(MathModule.Isnan(double.NaN));
        }

        [Fact]
        public void Isnan_NormalNumber_ReturnsFalse()
        {
            Assert.False(MathModule.Isnan(1.0));
        }

        [Fact]
        public void Isnan_Infinity_ReturnsFalse()
        {
            Assert.False(MathModule.Isnan(double.PositiveInfinity));
        }

        // --- Copysign ---

        [Fact]
        public void Copysign_PositiveMagnitudeNegativeSign_ReturnsNegative()
        {
            Assert.Equal(-1.0, MathModule.Copysign(1.0, -2.0));
        }

        [Fact]
        public void Copysign_PositiveMagnitudePositiveSign_ReturnsPositive()
        {
            Assert.Equal(5.0, MathModule.Copysign(5.0, 3.0));
        }

        [Fact]
        public void Copysign_NegativeMagnitudePositiveSign_ReturnsPositive()
        {
            Assert.Equal(3.0, MathModule.Copysign(-3.0, 1.0));
        }

        // --- GCD ---

        [Fact]
        public void Gcd_TwelveAndEight_ReturnsFour()
        {
            Assert.Equal(4L, MathModule.Gcd(12L, 8L));
        }

        [Fact]
        public void Gcd_CoprimePair_ReturnsOne()
        {
            Assert.Equal(1L, MathModule.Gcd(7L, 11L));
        }

        [Fact]
        public void Gcd_ZeroAndN_ReturnsN()
        {
            // gcd(0, n) = n
            Assert.Equal(5L, MathModule.Gcd(0L, 5L));
        }

        [Fact]
        public void Gcd_NegativeValues_ReturnsPositive()
        {
            Assert.Equal(4L, MathModule.Gcd(-12L, 8L));
        }

        // --- Factorial ---

        [Fact]
        public void Factorial_Zero_ReturnsOne()
        {
            Assert.Equal(1L, MathModule.Factorial(0));
        }

        [Fact]
        public void Factorial_Five_Returns120()
        {
            Assert.Equal(120L, MathModule.Factorial(5));
        }

        [Fact]
        public void Factorial_Negative_ThrowsValueError()
        {
            Assert.Throws<ValueError>(() => MathModule.Factorial(-1));
        }

        [Fact]
        public void Factorial_TooLarge_ThrowsOverflowError()
        {
            // n > 20 overflows long — Sharpy throws OverflowError
            Assert.Throws<OverflowError>(() => MathModule.Factorial(21));
        }

        // --- Log of zero returns NegativeInfinity in .NET (unlike Python which throws) ---

        [Fact]
        public void Log_Zero_ReturnsNegativeInfinity()
        {
            // .NET System.MathModule.Log(0) returns -∞; Python raises ValueError
            Assert.Equal(double.NegativeInfinity, MathModule.Log(0.0));
        }

        // --- Fsum additional cases ---

        [Fact]
        public void Fsum_NegativeValues_SumsCorrectly()
        {
            Assert.Equal(-6.0, MathModule.Fsum(new double[] { -1.0, -2.0, -3.0 }));
        }

        // --- Isclose edge cases (complementary to existing tests) ---

        [Fact]
        public void Isclose_FloatingPointApproximation_ReturnsTrue()
        {
            // 0.1 + 0.2 is not exactly 0.3 but isclose should pass with default tolerance
            Assert.True(MathModule.Isclose(0.1 + 0.2, 0.3));
        }
    }
}
