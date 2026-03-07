using Xunit;

namespace Sharpy.Tests
{
    public class MathAdditionalTests
    {
        // --- Lcm ---

        [Fact]
        public void Lcm_BasicValues()
        {
            Assert.Equal(12, Math.Lcm(4, 6));
            Assert.Equal(12, Math.Lcm(6, 4));
        }

        [Fact]
        public void Lcm_CoprimePair()
        {
            Assert.Equal(35, Math.Lcm(5, 7));
        }

        [Fact]
        public void Lcm_OneIsMultipleOfOther()
        {
            Assert.Equal(12, Math.Lcm(4, 12));
        }

        [Fact]
        public void Lcm_WithZero()
        {
            Assert.Equal(0, Math.Lcm(0, 5));
            Assert.Equal(0, Math.Lcm(5, 0));
            Assert.Equal(0, Math.Lcm(0, 0));
        }

        [Fact]
        public void Lcm_NegativeValues()
        {
            Assert.Equal(12, Math.Lcm(-4, 6));
            Assert.Equal(12, Math.Lcm(4, -6));
            Assert.Equal(12, Math.Lcm(-4, -6));
        }

        [Fact]
        public void Lcm_SameValues()
        {
            Assert.Equal(7, Math.Lcm(7, 7));
        }

        // --- Isclose ---

        [Fact]
        public void Isclose_EqualValues()
        {
            Assert.True(Math.Isclose(1.0, 1.0));
        }

        [Fact]
        public void Isclose_CloseValues()
        {
            Assert.True(Math.Isclose(1.0, 1.0 + 1e-10));
        }

        [Fact]
        public void Isclose_NotCloseValues()
        {
            Assert.False(Math.Isclose(1.0, 2.0));
        }

        [Fact]
        public void Isclose_WithAbsTol()
        {
            Assert.True(Math.Isclose(0.0, 0.001, abs_tol: 0.01));
            Assert.False(Math.Isclose(0.0, 0.001, abs_tol: 0.0001));
        }

        [Fact]
        public void Isclose_WithRelTol()
        {
            Assert.True(Math.Isclose(100.0, 100.1, rel_tol: 0.01));
            Assert.False(Math.Isclose(100.0, 102.0, rel_tol: 0.01));
        }

        [Fact]
        public void Isclose_Infinity()
        {
            Assert.True(Math.Isclose(double.PositiveInfinity, double.PositiveInfinity));
            Assert.False(Math.Isclose(double.PositiveInfinity, 1.0));
            Assert.False(Math.Isclose(1.0, double.NegativeInfinity));
        }

        [Fact]
        public void Isclose_NaN()
        {
            Assert.False(Math.Isclose(double.NaN, double.NaN));
            Assert.False(Math.Isclose(double.NaN, 1.0));
        }

        [Fact]
        public void Isclose_NegativeTolerance_Throws()
        {
            Assert.Throws<ValueError>(() => Math.Isclose(1.0, 1.0, rel_tol: -1.0));
            Assert.Throws<ValueError>(() => Math.Isclose(1.0, 1.0, abs_tol: -1.0));
        }

        // --- Comb ---

        [Fact]
        public void Comb_BasicValues()
        {
            Assert.Equal(10, Math.Comb(5, 2));
            Assert.Equal(1, Math.Comb(5, 0));
            Assert.Equal(1, Math.Comb(5, 5));
            Assert.Equal(5, Math.Comb(5, 1));
        }

        [Fact]
        public void Comb_KGreaterThanN()
        {
            Assert.Equal(0, Math.Comb(3, 5));
        }

        [Fact]
        public void Comb_NegativeN_Throws()
        {
            Assert.Throws<ValueError>(() => Math.Comb(-1, 2));
        }

        [Fact]
        public void Comb_NegativeK_Throws()
        {
            Assert.Throws<ValueError>(() => Math.Comb(5, -1));
        }

        [Fact]
        public void Comb_LargerValues()
        {
            // C(10, 3) = 120
            Assert.Equal(120, Math.Comb(10, 3));
            // C(20, 10) = 184756
            Assert.Equal(184756, Math.Comb(20, 10));
        }

        // --- Perm ---

        [Fact]
        public void Perm_BasicValues()
        {
            // P(5, 2) = 20
            Assert.Equal(20, Math.Perm(5, 2));
            Assert.Equal(1, Math.Perm(5, 0));
            Assert.Equal(120, Math.Perm(5, 5));
        }

        [Fact]
        public void Perm_KGreaterThanN()
        {
            Assert.Equal(0, Math.Perm(3, 5));
        }

        [Fact]
        public void Perm_NegativeN_Throws()
        {
            Assert.Throws<ValueError>(() => Math.Perm(-1, 2));
        }

        [Fact]
        public void Perm_NegativeK_Throws()
        {
            Assert.Throws<ValueError>(() => Math.Perm(5, -1));
        }

        [Fact]
        public void Perm_SingleArg_ReturnsFactorial()
        {
            Assert.Equal(120, Math.Perm(5));
            Assert.Equal(1, Math.Perm(0));
            Assert.Equal(1, Math.Perm(1));
        }

        // --- Fsum ---

        [Fact]
        public void Fsum_BasicValues()
        {
            Assert.Equal(6.0, Math.Fsum(new double[] { 1.0, 2.0, 3.0 }));
        }

        [Fact]
        public void Fsum_AccuracyTest()
        {
            // Python: math.fsum([0.1] * 10) == 1.0
            var values = new double[10];
            for (int i = 0; i < 10; i++)
                values[i] = 0.1;
            Assert.Equal(1.0, Math.Fsum(values));
        }

        [Fact]
        public void Fsum_EmptyIterable()
        {
            Assert.Equal(0.0, Math.Fsum(new double[] { }));
        }

        [Fact]
        public void Fsum_SingleValue()
        {
            Assert.Equal(42.5, Math.Fsum(new double[] { 42.5 }));
        }

        // --- Prod ---

        [Fact]
        public void Prod_DoubleValues()
        {
            Assert.Equal(24.0, Math.Prod(new double[] { 1.0, 2.0, 3.0, 4.0 }));
        }

        [Fact]
        public void Prod_IntValues()
        {
            Assert.Equal(24, Math.Prod(new int[] { 1, 2, 3, 4 }));
        }

        [Fact]
        public void Prod_WithStart()
        {
            Assert.Equal(48.0, Math.Prod(new double[] { 2.0, 3.0, 4.0 }, start: 2.0));
        }

        [Fact]
        public void Prod_EmptyIterable()
        {
            Assert.Equal(1.0, Math.Prod(new double[] { }));
            Assert.Equal(1, Math.Prod(new int[] { }));
        }

        [Fact]
        public void Prod_WithZero()
        {
            Assert.Equal(0.0, Math.Prod(new double[] { 1.0, 0.0, 3.0 }));
        }

        // --- Hypot ---

        [Fact]
        public void Hypot_ThreeFourFive()
        {
            Assert.Equal(5.0, Math.Hypot(3.0, 4.0));
        }

        [Fact]
        public void Hypot_ZeroValues()
        {
            Assert.Equal(0.0, Math.Hypot(0.0, 0.0));
            Assert.Equal(3.0, Math.Hypot(3.0, 0.0));
        }

        [Fact]
        public void Hypot_NegativeValues()
        {
            Assert.Equal(5.0, Math.Hypot(-3.0, 4.0));
            Assert.Equal(5.0, Math.Hypot(3.0, -4.0));
        }

        // --- Expm1 ---

        [Fact]
        public void Expm1_Zero()
        {
            Assert.True(Math.Isclose(0.0, Math.Expm1(0.0), abs_tol: 1e-15));
        }

        [Fact]
        public void Expm1_SmallValue()
        {
            // For small x, expm1(x) ≈ x
            double result = Math.Expm1(1e-10);
            Assert.True(Math.Isclose(1e-10, result, rel_tol: 1e-6));
        }

        [Fact]
        public void Expm1_LargerValue()
        {
            // expm1(1) = e - 1 ≈ 1.718281828
            Assert.True(Math.Isclose(System.Math.E - 1.0, Math.Expm1(1.0), rel_tol: 1e-9));
        }

        // --- Log1p ---

        [Fact]
        public void Log1p_Zero()
        {
            Assert.Equal(0.0, Math.Log1p(0.0));
        }

        [Fact]
        public void Log1p_SmallValue()
        {
            // For small x, log1p(x) ≈ x
            double result = Math.Log1p(1e-10);
            Assert.True(Math.Isclose(1e-10, result, rel_tol: 1e-6));
        }

        [Fact]
        public void Log1p_LargerValue()
        {
            // log1p(e-1) = 1.0
            Assert.True(Math.Isclose(1.0, Math.Log1p(System.Math.E - 1.0), rel_tol: 1e-9));
        }

        [Fact]
        public void Log1p_NegativeOne_Throws()
        {
            Assert.Throws<ValueError>(() => Math.Log1p(-1.0));
        }

        [Fact]
        public void Log1p_BelowNegativeOne_Throws()
        {
            Assert.Throws<ValueError>(() => Math.Log1p(-2.0));
        }

        // --- Remainder ---

        [Fact]
        public void Remainder_BasicValues()
        {
            // math.remainder(10, 3) == 1.0 (IEEE: 10 - 3*round(10/3) = 10 - 9 = 1)
            Assert.Equal(1.0, Math.Remainder(10.0, 3.0));
        }

        [Fact]
        public void Remainder_NegativeResult()
        {
            // math.remainder(11, 3) == -1.0 (IEEE: 11 - 3*round(11/3) = 11 - 12 = -1)
            Assert.Equal(-1.0, Math.Remainder(11.0, 3.0));
        }

        [Fact]
        public void Remainder_ZeroDivisor_Throws()
        {
            Assert.Throws<ValueError>(() => Math.Remainder(10.0, 0.0));
        }

        [Fact]
        public void Remainder_ZeroDividend()
        {
            Assert.Equal(0.0, Math.Remainder(0.0, 3.0));
        }
    }
}
