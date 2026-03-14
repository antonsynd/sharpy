using System;

namespace Sharpy
{
    /// <summary>
    /// Mathematical functions, similar to Python's math module.
    /// This module provides access to mathematical functions defined by the C standard.
    /// </summary>
    public static partial class Math
    {
        /// <summary>
        /// The mathematical constant π = 3.141592..., to available precision.
        /// </summary>
        public const double Pi = System.Math.PI;

        /// <summary>
        /// The mathematical constant e = 2.718281..., to available precision.
        /// </summary>
        public const double E = System.Math.E;

        /// <summary>
        /// The mathematical constant τ = 2π = 6.283185..., to available precision.
        /// </summary>
        public const double Tau = 2 * System.Math.PI;

        /// <summary>
        /// Positive infinity.
        /// </summary>
        public const double Inf = double.PositiveInfinity;

        /// <summary>
        /// A floating-point "not a number" (NaN) value.
        /// </summary>
        // Note: double.NaN cannot be a 'const' in C#, so this must be 'static readonly' for consistency with language rules.
        public static readonly double Nan = double.NaN;

        /// <summary>
        /// Return the ceiling of x, the smallest integer greater than or equal to x.
        /// </summary>
        /// <param name="x">The value to ceil.</param>
        /// <returns>The smallest integer greater than or equal to <paramref name="x"/>.</returns>
        /// <example>
        /// <code>
        /// math.ceil(3.2)    # 4.0
        /// math.ceil(-0.5)   # 0.0
        /// </code>
        /// </example>
        public static double Ceil(double x) => System.Math.Ceiling(x);

        /// <summary>
        /// Return the floor of x, the largest integer less than or equal to x.
        /// </summary>
        /// <param name="x">The value to floor.</param>
        /// <returns>The largest integer less than or equal to <paramref name="x"/>.</returns>
        /// <example>
        /// <code>
        /// math.floor(3.7)    # 3.0
        /// math.floor(-0.5)   # -1.0
        /// </code>
        /// </example>
        public static double Floor(double x) => System.Math.Floor(x);

        /// <summary>
        /// Return the absolute value of x as a float.
        /// </summary>
        /// <param name="x">The value</param>
        /// <returns>The absolute value of <paramref name="x"/></returns>
        public static double Fabs(double x) => System.Math.Abs(x);

        /// <summary>
        /// Return x with the sign of y.
        /// </summary>
        /// <param name="x">The magnitude</param>
        /// <param name="y">The value whose sign is used</param>
        /// <returns><paramref name="x"/> with the sign of <paramref name="y"/></returns>
        public static double Copysign(double x, double y)
        {
            // Polyfill for Math.CopySign (not available in netstandard2.x)
            return System.Math.Abs(x) * (y < 0 || (y == 0 && 1.0 / y < 0) ? -1 : 1);
        }

        /// <summary>
        /// Return the square root of x.
        /// </summary>
        /// <param name="x">The value to compute the square root of.</param>
        /// <returns>The square root of <paramref name="x"/>.</returns>
        /// <example>
        /// <code>
        /// math.sqrt(16.0)    # 4.0
        /// math.sqrt(2.0)     # 1.4142135623730951
        /// </code>
        /// </example>
        public static double Sqrt(double x) => System.Math.Sqrt(x);

        /// <summary>
        /// Return x raised to the power y.
        /// </summary>
        /// <param name="x">The base.</param>
        /// <param name="y">The exponent.</param>
        /// <returns><paramref name="x"/> raised to the power <paramref name="y"/>.</returns>
        public static double Pow(double x, double y) => System.Math.Pow(x, y);

        /// <summary>
        /// Return e raised to the power x.
        /// </summary>
        /// <param name="x">The exponent.</param>
        /// <returns>e raised to the power <paramref name="x"/>.</returns>
        public static double Exp(double x) => System.Math.Exp(x);

        /// <summary>
        /// Return the natural logarithm of x (to base e).
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The natural logarithm of <paramref name="x"/>.</returns>
        public static double Log(double x) => System.Math.Log(x);

        /// <summary>
        /// Return the logarithm of x to the given base.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <param name="baseValue">The logarithm base.</param>
        /// <returns>The logarithm of <paramref name="x"/> to base <paramref name="baseValue"/>.</returns>
        public static double Log(double x, double baseValue) => System.Math.Log(x, baseValue);

        /// <summary>
        /// Return the base-10 logarithm of x.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The base-10 logarithm of <paramref name="x"/>.</returns>
        public static double Log10(double x) => System.Math.Log10(x);

        /// <summary>
        /// Return the base-2 logarithm of x.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The base-2 logarithm of <paramref name="x"/>.</returns>
        public static double Log2(double x) => System.Math.Log(x, 2.0);

        /// <summary>
        /// Return the sine of x radians.
        /// </summary>
        /// <param name="x">The angle in radians.</param>
        /// <returns>The sine of <paramref name="x"/>.</returns>
        public static double Sin(double x) => System.Math.Sin(x);

        /// <summary>
        /// Return the cosine of x radians.
        /// </summary>
        /// <param name="x">The angle in radians.</param>
        /// <returns>The cosine of <paramref name="x"/>.</returns>
        public static double Cos(double x) => System.Math.Cos(x);

        /// <summary>
        /// Return the tangent of x radians.
        /// </summary>
        /// <param name="x">The angle in radians.</param>
        /// <returns>The tangent of <paramref name="x"/>.</returns>
        public static double Tan(double x) => System.Math.Tan(x);

        /// <summary>
        /// Return the arc sine of x, in radians.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The arc sine of <paramref name="x"/> in radians.</returns>
        public static double Asin(double x) => System.Math.Asin(x);

        /// <summary>
        /// Return the arc cosine of x, in radians.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The arc cosine of <paramref name="x"/> in radians.</returns>
        public static double Acos(double x) => System.Math.Acos(x);

        /// <summary>
        /// Return the arc tangent of x, in radians.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The arc tangent of <paramref name="x"/> in radians.</returns>
        public static double Atan(double x) => System.Math.Atan(x);

        /// <summary>
        /// Return the arc tangent of y/x, in radians.
        /// </summary>
        /// <param name="y">The y coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        /// <returns>The arc tangent of <paramref name="y"/>/<paramref name="x"/> in radians.</returns>
        public static double Atan2(double y, double x) => System.Math.Atan2(y, x);

        /// <summary>
        /// Return the hyperbolic sine of x.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The hyperbolic sine of <paramref name="x"/>.</returns>
        public static double Sinh(double x) => System.Math.Sinh(x);

        /// <summary>
        /// Return the hyperbolic cosine of x.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The hyperbolic cosine of <paramref name="x"/>.</returns>
        public static double Cosh(double x) => System.Math.Cosh(x);

        /// <summary>
        /// Return the hyperbolic tangent of x.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The hyperbolic tangent of <paramref name="x"/>.</returns>
        public static double Tanh(double x) => System.Math.Tanh(x);

        /// <summary>
        /// Convert angle x from radians to degrees.
        /// </summary>
        /// <param name="x">The angle in radians.</param>
        /// <returns>The angle in degrees.</returns>
        public static double Degrees(double x) => x * (180.0 / System.Math.PI);

        /// <summary>
        /// Convert angle x from degrees to radians.
        /// </summary>
        /// <param name="x">The angle in degrees.</param>
        /// <returns>The angle in radians.</returns>
        public static double Radians(double x) => x * (System.Math.PI / 180.0);

        /// <summary>
        /// Return True if x is neither an infinity nor a NaN, and False otherwise.
        /// </summary>
        /// <param name="x">The value to check.</param>
        /// <returns><c>true</c> if <paramref name="x"/> is finite; otherwise <c>false</c>.</returns>
        public static bool Isfinite(double x) => !double.IsInfinity(x) && !double.IsNaN(x);

        /// <summary>
        /// Return True if x is a positive or negative infinity, and False otherwise.
        /// </summary>
        /// <param name="x">The value to check.</param>
        /// <returns><c>true</c> if <paramref name="x"/> is infinite; otherwise <c>false</c>.</returns>
        public static bool Isinf(double x) => double.IsInfinity(x);

        /// <summary>
        /// Return True if x is a NaN (not a number), and False otherwise.
        /// </summary>
        /// <param name="x">The value to check.</param>
        /// <returns><c>true</c> if <paramref name="x"/> is NaN; otherwise <c>false</c>.</returns>
        public static bool Isnan(double x) => double.IsNaN(x);

        /// <summary>
        /// Return the integer part of x, removing all fractional digits.
        /// </summary>
        /// <param name="x">The value to truncate.</param>
        /// <returns>The integer part of <paramref name="x"/>.</returns>
        public static double Trunc(double x) => System.Math.Truncate(x);

        /// <summary>
        /// Return e raised to the power x, minus 1. Accurate for small x.
        /// </summary>
        /// <param name="x">The exponent.</param>
        /// <returns>e raised to the power <paramref name="x"/>, minus 1.</returns>
        public static double Expm1(double x)
        {
            // For small values, use Taylor series for better accuracy
            if (System.Math.Abs(x) < 1e-5)
            {
                return x + 0.5 * x * x + x * x * x / 6.0;
            }

            return System.Math.Exp(x) - 1.0;
        }

        /// <summary>
        /// Return the natural logarithm of 1+x (base e). Accurate for small x.
        /// </summary>
        /// <param name="x">The value (must be greater than -1).</param>
        /// <returns>The natural logarithm of 1 + <paramref name="x"/>.</returns>
        /// <exception cref="ValueError">Thrown if <paramref name="x"/> is less than or equal to -1.</exception>
        public static double Log1p(double x)
        {
            if (x <= -1.0)
            {
                throw new ValueError("math domain error");
            }

            // For small values, use series for better accuracy
            if (System.Math.Abs(x) < 1e-4)
            {
                // log(1+x) = x - x^2/2 + x^3/3 - x^4/4 + ...
                double x2 = x * x;
                return x - x2 / 2.0 + x2 * x / 3.0 - x2 * x2 / 4.0;
            }

            return System.Math.Log(1.0 + x);
        }

        /// <summary>
        /// Return the IEEE 754-style remainder of x with respect to y.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The IEEE 754-style remainder of <paramref name="x"/> / <paramref name="y"/>.</returns>
        /// <exception cref="ValueError">Thrown if <paramref name="y"/> is zero.</exception>
        public static double Remainder(double x, double y)
        {
            if (y == 0.0)
            {
                throw new ValueError("math domain error");
            }

            return System.Math.IEEERemainder(x, y);
        }
    }
}
