using System;
using System.Globalization;
using System.Numerics;

namespace Sharpy
{
    /// <summary>
    /// <c>round()</c> per CPython's <c>float.__round__</c> / <c>int.__round__</c> (#2460).
    /// </summary>
    /// <remarks>
    /// <para><c>round(x)</c> rounds half to even and returns an integer; NaN raises <see cref="ValueError"/>,
    /// an infinity or a result outside the return type raises <see cref="OverflowError"/> (never a wrapped
    /// cast: <c>int</c> is <c>System.Int32</c>, Axiom 1, the same refusal as <c>int(float)</c> and R-IS).</para>
    /// <para><c>round(x, n)</c> takes any <c>n</c>. A float is correctly rounded the way CPython's
    /// <c>double_round</c> does it: the exact binary value is rounded half to even at the n-th decimal
    /// place and the decimal result is parsed back (so <c>round(2.675, 2) == 2.67</c>: 2.675 is below the
    /// tie in binary). NaN and infinities round to themselves; <c>n</c> past the precision returns
    /// <c>x</c>; <c>n</c> below the exponent range returns a signed zero; a rounded value past the
    /// type's range raises <see cref="OverflowError"/>.</para>
    /// </remarks>
    public static partial class Builtins
    {
        // CPython's NDIGITS_MAX / NDIGITS_MIN for IEEE double: (DBL_MANT_DIG - DBL_MIN_EXP) * 0.30103 and
        // -(DBL_MAX_EXP + 1) * 0.30103.
        private const int DoubleNDigitsMax = 323;
        private const int DoubleNDigitsMin = -308;

        // The same bounds for IEEE single: (FLT_MANT_DIG - FLT_MIN_EXP) * 0.30103 and -(FLT_MAX_EXP + 1) * 0.30103.
        private const int SingleNDigitsMax = 44;
        private const int SingleNDigitsMin = -38;

        /// <summary>
        /// Round a number to the nearest integer, half to even.
        /// </summary>
        /// <param name="x">The number to round</param>
        /// <returns>The rounded value</returns>
        /// <exception cref="ValueError">x is NaN.</exception>
        /// <exception cref="OverflowError">x is infinite, or the rounded value does not fit an <c>int</c>.</exception>
        /// <example>
        /// <code>
        /// round(3.7)       # 4
        /// round(2.5)       # 2 (half to even)
        /// round(3.14159, 2) # 3.14
        /// </code>
        /// </example>
        public static int Round(double x)
        {
            double rounded = RoundToIntegralValue(x);
            if (rounded < int.MinValue || rounded > int.MaxValue)
                throw new OverflowError("cannot convert float " + Str(rounded) + " to int: out of range");
            return (int)rounded;
        }

        /// <summary>
        /// Round a number to n decimal places (n may be negative), correctly rounded, half to even.
        /// </summary>
        /// <param name="x">The number to round</param>
        /// <param name="n">The number of decimal places</param>
        /// <returns>The rounded value</returns>
        /// <exception cref="OverflowError">The rounded value is too large to represent.</exception>
        public static double Round(double x, int n)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
                return x;
            if (n > DoubleNDigitsMax)
                return x;
            if (n < DoubleNDigitsMin)
                return 0.0 * x;
            double rounded = ParseRounded(RoundedDecimalText(Math.Abs(x), n), isSingle: false);
            return double.IsNegative(x) ? -rounded : rounded;
        }

        /// <summary>
        /// Round a float to the nearest integer, half to even.
        /// </summary>
        /// <param name="x">The number to round</param>
        /// <returns>The rounded value</returns>
        /// <exception cref="ValueError">x is NaN.</exception>
        /// <exception cref="OverflowError">x is infinite, or the rounded value does not fit an <c>int</c>.</exception>
        public static int Round(float x)
        {
            return Round((double)x);
        }

        /// <summary>
        /// Round a float to n decimal places (n may be negative), correctly rounded, half to even.
        /// </summary>
        /// <param name="x">The number to round</param>
        /// <param name="n">The number of decimal places</param>
        /// <returns>The rounded value</returns>
        /// <exception cref="OverflowError">The rounded value is too large to represent.</exception>
        public static float Round(float x, int n)
        {
            if (float.IsNaN(x) || float.IsInfinity(x))
                return x;
            if (n > SingleNDigitsMax)
                return x;
            if (n < SingleNDigitsMin)
                return 0.0f * x;
            // float -> double is exact, so the decimal text is the single's own exact value rounded once;
            // parsing it straight to float avoids a second (double) rounding.
            float rounded = (float)ParseRounded(RoundedDecimalText(Math.Abs((double)x), n), isSingle: true);
            return double.IsNegative(x) ? -rounded : rounded;
        }

        /// <summary>
        /// Round a decimal to the nearest integer, half to even.
        /// </summary>
        /// <param name="x">The number to round</param>
        /// <returns>The rounded value</returns>
        /// <exception cref="OverflowError">The rounded value does not fit an <c>int</c>.</exception>
        public static int Round(decimal x)
        {
            decimal rounded = Math.Round(x, MidpointRounding.ToEven);
            if (rounded < int.MinValue || rounded > int.MaxValue)
                throw new OverflowError("cannot convert decimal " + rounded.ToString(CultureInfo.InvariantCulture) + " to int: out of range");
            return (int)rounded;
        }

        /// <summary>
        /// Round a decimal to n decimal places (n may be negative), exactly, half to even.
        /// </summary>
        /// <param name="x">The number to round</param>
        /// <param name="n">The number of decimal places</param>
        /// <returns>The rounded value</returns>
        /// <exception cref="OverflowError">The rounded value is too large for a decimal.</exception>
        public static decimal Round(decimal x, int n)
        {
            int[] bits = decimal.GetBits(x);
            int scale = (bits[3] >> 16) & 0xFF;
            bool negative = (bits[3] & unchecked((int)0x80000000)) != 0;
            if (n >= scale)
                return x;
            var mantissa = new BigInteger((uint)bits[0])
                | (new BigInteger((uint)bits[1]) << 32)
                | (new BigInteger((uint)bits[2]) << 64);
            // value = mantissa * 10^-scale; keep (scale - n) fewer digits.
            BigInteger q = DivideHalfToEven(mantissa, BigInteger.Pow(10, scale - n));
            if (n >= 0)
            {
                // q < mantissa, so it fits the 96-bit mantissa at scale n.
                byte[] qBytes = q.ToByteArray();
                var words = new int[3];
                for (int i = 0; i < qBytes.Length && i < 12; i++)
                    words[i / 4] |= qBytes[i] << (8 * (i % 4));
                return new decimal(words[0], words[1], words[2], negative, (byte)n);
            }
            BigInteger magnitude = q * BigInteger.Pow(10, -n);
            if (magnitude > new BigInteger(decimal.MaxValue))
                throw new OverflowError("rounded value too large to represent");
            decimal result = (decimal)magnitude;
            return negative ? -result : result;
        }

        /// <summary>
        /// Round an int to n decimal places: n &gt;= 0 returns x; a negative n rounds half to even at 10^-n.
        /// </summary>
        /// <param name="x">The number to round</param>
        /// <param name="n">The number of decimal places</param>
        /// <returns>The rounded value</returns>
        /// <exception cref="OverflowError">The rounded value does not fit an <c>int</c>.</exception>
        /// <example>
        /// <code>
        /// round(15, -1)   # 20
        /// round(25, -1)   # 20 (half to even)
        /// </code>
        /// </example>
        public static int Round(int x, int n)
        {
            BigInteger rounded = RoundInteger(x, n);
            if (rounded < int.MinValue || rounded > int.MaxValue)
                throw new OverflowError("rounded value " + rounded.ToString(CultureInfo.InvariantCulture) + " does not fit int");
            return (int)rounded;
        }

        /// <summary>
        /// Round a long to n decimal places: n &gt;= 0 returns x; a negative n rounds half to even at 10^-n.
        /// </summary>
        /// <param name="x">The number to round</param>
        /// <param name="n">The number of decimal places</param>
        /// <returns>The rounded value</returns>
        /// <exception cref="OverflowError">The rounded value does not fit a <c>long</c>.</exception>
        public static long Round(long x, int n)
        {
            BigInteger rounded = RoundInteger(x, n);
            if (rounded < long.MinValue || rounded > long.MaxValue)
                throw new OverflowError("rounded value " + rounded.ToString(CultureInfo.InvariantCulture) + " does not fit int64");
            return (long)rounded;
        }

        /// <summary>Round an int to the nearest integer: x itself.</summary>
        /// <param name="x">The number to round</param>
        /// <returns>x</returns>
        public static int Round(int x)
        {
            return x;
        }

        /// <summary>Round a long to the nearest integer: x itself.</summary>
        /// <param name="x">The number to round</param>
        /// <returns>x</returns>
        public static long Round(long x)
        {
            return x;
        }

        // CPython float.__round__ with ndigits None: half to even, NaN -> ValueError, +-inf -> OverflowError.
        private static double RoundToIntegralValue(double x)
        {
            if (double.IsNaN(x))
                throw new ValueError("cannot convert float NaN to integer");
            if (double.IsInfinity(x))
                throw new OverflowError("cannot convert float infinity to integer");
            return Math.Round(x, MidpointRounding.ToEven);
        }

        // int.__round__: half to even on the integer itself.
        private static BigInteger RoundInteger(BigInteger x, int n)
        {
            if (n >= 0)
                return x;
            BigInteger q = DivideHalfToEven(BigInteger.Abs(x), BigInteger.Pow(10, -n));
            BigInteger magnitude = q * BigInteger.Pow(10, -n);
            return x.Sign < 0 ? -magnitude : magnitude;
        }

        // The exact value of a finite, non-negative double, rounded half to even at the n-th decimal place,
        // as decimal text "<digits>E<exponent>" (the job of _Py_dg_dtoa mode 3 in CPython's double_round).
        private static string RoundedDecimalText(double absX, int n)
        {
            long bits = BitConverter.DoubleToInt64Bits(absX);
            int biasedExponent = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & ((1L << 52) - 1);
            long mantissa = biasedExponent == 0 ? fraction : fraction | (1L << 52);
            int exponent = biasedExponent == 0 ? -1074 : biasedExponent - 1075;

            // absX = mantissa * 2^exponent; scaled = absX * 10^n = numerator / denominator.
            BigInteger numerator = mantissa;
            BigInteger denominator = BigInteger.One;
            if (n >= 0)
                numerator *= BigInteger.Pow(10, n);
            else
                denominator = BigInteger.Pow(10, -n);
            if (exponent >= 0)
                numerator <<= exponent;
            else
                denominator <<= -exponent;

            BigInteger digits = DivideHalfToEven(numerator, denominator);
            return digits.ToString(CultureInfo.InvariantCulture) + "E" + (-n).ToString(CultureInfo.InvariantCulture);
        }

        // Correctly rounded parse of the decimal text; a result past the type's range is CPython's OverflowError.
        private static double ParseRounded(string text, bool isSingle)
        {
            double result;
            try
            {
                result = isSingle
                    ? float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)
                    : double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            catch (System.OverflowException)
            {
                result = double.PositiveInfinity;
            }
            if (double.IsInfinity(result))
                throw new OverflowError("rounded value too large to represent");
            return result;
        }

        // numerator / denominator (both non-negative), rounded half to even.
        private static BigInteger DivideHalfToEven(BigInteger numerator, BigInteger denominator)
        {
            BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
            int cmp = (remainder << 1).CompareTo(denominator);
            if (cmp > 0 || (cmp == 0 && !quotient.IsEven))
                quotient += BigInteger.One;
            return quotient;
        }
    }
}
