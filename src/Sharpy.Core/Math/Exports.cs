namespace Sharpy.Math;

using Sharpy.Core;

/// <summary>
/// Mathematical functions, similar to Python's math module.
/// This module provides access to mathematical functions defined by the C standard.
/// </summary>
public static class Exports
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
    public static readonly double Nan = double.NaN;

    /// <summary>
    /// Return the ceiling of x, the smallest integer greater than or equal to x.
    /// </summary>
    public static double Ceil(double x) => System.Math.Ceiling(x);

    /// <summary>
    /// Return the floor of x, the largest integer less than or equal to x.
    /// </summary>
    public static double Floor(double x) => System.Math.Floor(x);

    /// <summary>
    /// Return the absolute value of x.
    /// </summary>
    public static double Fabs(double x) => System.Math.Abs(x);

    /// <summary>
    /// Return x with the sign of y.
    /// </summary>
    public static double Copysign(double x, double y)
    {
        return System.Math.CopySign(x, y);
    }

    /// <summary>
    /// Return the square root of x.
    /// </summary>
    public static double Sqrt(double x) => System.Math.Sqrt(x);

    /// <summary>
    /// Return x raised to the power y.
    /// </summary>
    public static double Pow(double x, double y) => System.Math.Pow(x, y);

    /// <summary>
    /// Return e raised to the power x.
    /// </summary>
    public static double Exp(double x) => System.Math.Exp(x);

    /// <summary>
    /// Return the natural logarithm of x (to base e).
    /// </summary>
    public static double Log(double x) => System.Math.Log(x);

    /// <summary>
    /// Return the logarithm of x to the given base.
    /// </summary>
    public static double Log(double x, double baseValue) => System.Math.Log(x, baseValue);

    /// <summary>
    /// Return the base-10 logarithm of x.
    /// </summary>
    public static double Log10(double x) => System.Math.Log10(x);

    /// <summary>
    /// Return the base-2 logarithm of x.
    /// </summary>
    public static double Log2(double x) => System.Math.Log2(x);

    /// <summary>
    /// Return the sine of x radians.
    /// </summary>
    public static double Sin(double x) => System.Math.Sin(x);

    /// <summary>
    /// Return the cosine of x radians.
    /// </summary>
    public static double Cos(double x) => System.Math.Cos(x);

    /// <summary>
    /// Return the tangent of x radians.
    /// </summary>
    public static double Tan(double x) => System.Math.Tan(x);

    /// <summary>
    /// Return the arc sine of x, in radians.
    /// </summary>
    public static double Asin(double x) => System.Math.Asin(x);

    /// <summary>
    /// Return the arc cosine of x, in radians.
    /// </summary>
    public static double Acos(double x) => System.Math.Acos(x);

    /// <summary>
    /// Return the arc tangent of x, in radians.
    /// </summary>
    public static double Atan(double x) => System.Math.Atan(x);

    /// <summary>
    /// Return the arc tangent of y/x, in radians.
    /// </summary>
    public static double Atan2(double y, double x) => System.Math.Atan2(y, x);

    /// <summary>
    /// Return the hyperbolic sine of x.
    /// </summary>
    public static double Sinh(double x) => System.Math.Sinh(x);

    /// <summary>
    /// Return the hyperbolic cosine of x.
    /// </summary>
    public static double Cosh(double x) => System.Math.Cosh(x);

    /// <summary>
    /// Return the hyperbolic tangent of x.
    /// </summary>
    public static double Tanh(double x) => System.Math.Tanh(x);

    /// <summary>
    /// Convert angle x from radians to degrees.
    /// </summary>
    public static double Degrees(double x) => x * (180.0 / System.Math.PI);

    /// <summary>
    /// Convert angle x from degrees to radians.
    /// </summary>
    public static double Radians(double x) => x * (System.Math.PI / 180.0);

    /// <summary>
    /// Return True if x is neither an infinity nor a NaN, and False otherwise.
    /// </summary>
    public static bool Isfinite(double x) => double.IsFinite(x);

    /// <summary>
    /// Return True if x is a positive or negative infinity, and False otherwise.
    /// </summary>
    public static bool Isinf(double x) => double.IsInfinity(x);

    /// <summary>
    /// Return True if x is a NaN (not a number), and False otherwise.
    /// </summary>
    public static bool Isnan(double x) => double.IsNaN(x);

    /// <summary>
    /// Return the integer part of x, removing all fractional digits.
    /// </summary>
    public static double Trunc(double x) => System.Math.Truncate(x);

    /// <summary>
    /// Return the Greatest Common Divisor of integers a and b.
    /// </summary>
    public static long Gcd(long a, long b)
    {
        a = System.Math.Abs(a);
        b = System.Math.Abs(b);

        while (b != 0)
        {
            long temp = b;
            b = a % b;
            a = temp;
        }

        return a;
    }

    /// <summary>
    /// Return the factorial of n. Raises ValueError if n is negative.
    /// </summary>
    public static long Factorial(int n)
    {
        if (n < 0)
        {
            throw new ValueError("factorial() not defined for negative values");
        }

        if (n == 0 || n == 1)
        {
            return 1;
        }

        long result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }

        return result;
    }
}
