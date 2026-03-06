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
            // Polyfill for Math.CopySign (not available in netstandard2.x)
            return System.Math.Abs(x) * (y < 0 || (y == 0 && 1.0 / y < 0) ? -1 : 1);
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
        public static double Log2(double x) => System.Math.Log(x, 2.0);

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
        public static bool Isfinite(double x) => !double.IsInfinity(x) && !double.IsNaN(x);

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
        /// Return the Least Common Multiple of integers a and b.
        /// </summary>
        public static long Lcm(long a, long b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }

            a = System.Math.Abs(a);
            b = System.Math.Abs(b);
            return a / Gcd(a, b) * b;
        }

        /// <summary>
        /// Determine whether two floating point numbers are close in value.
        /// </summary>
        public static bool Isclose(double a, double b, double rel_tol = 1e-9, double abs_tol = 0.0)
        {
            if (rel_tol < 0.0 || abs_tol < 0.0)
            {
                throw new ValueError("tolerances must be non-negative");
            }

            if (a == b)
            {
                return true;
            }

            if (double.IsInfinity(a) || double.IsInfinity(b))
            {
                return false;
            }

            double diff = System.Math.Abs(a - b);
            return diff <= System.Math.Max(rel_tol * System.Math.Max(System.Math.Abs(a), System.Math.Abs(b)), abs_tol);
        }

        /// <summary>
        /// Return the number of ways to choose k items from n items without repetition and without order.
        /// </summary>
        public static long Comb(int n, int k)
        {
            if (n < 0 || k < 0 || k > n)
            {
                return 0;
            }

            if (k == 0 || k == n)
            {
                return 1;
            }

            // Optimize by using smaller k
            if (k > n - k)
            {
                k = n - k;
            }

            long result = 1;
            for (int i = 0; i < k; i++)
            {
                result = result * (n - i) / (i + 1);
            }

            return result;
        }

        /// <summary>
        /// Return the number of ways to choose k items from n items without repetition and with order.
        /// </summary>
        public static long Perm(int n, int k)
        {
            if (n < 0 || k < 0 || k > n)
            {
                return 0;
            }

            long result = 1;
            for (int i = 0; i < k; i++)
            {
                result *= (n - i);
            }

            return result;
        }

        /// <summary>
        /// Return an accurate floating point sum of values in the iterable using Kahan summation.
        /// </summary>
        public static double Fsum(System.Collections.Generic.IEnumerable<double> iterable)
        {
            double sum = 0.0;
            double c = 0.0;
            foreach (double x in iterable)
            {
                double y = x - c;
                double t = sum + y;
                c = (t - sum) - y;
                sum = t;
            }

            return sum;
        }

        /// <summary>
        /// Return the product of all the elements in the input iterable.
        /// The default start value for the product is 1.
        /// </summary>
        public static int Prod(System.Collections.Generic.IEnumerable<int> iterable, int start = 1)
        {
            int result = start;
            foreach (int x in iterable)
            {
                result *= x;
            }

            return result;
        }

        /// <summary>
        /// Return the product of all the elements in the input iterable (double overload).
        /// </summary>
        public static double Prod(System.Collections.Generic.IEnumerable<double> iterable, double start = 1.0)
        {
            double result = start;
            foreach (double x in iterable)
            {
                result *= x;
            }

            return result;
        }

        /// <summary>
        /// Return the Euclidean distance, sqrt(x*x + y*y).
        /// </summary>
        public static double Hypot(double x, double y) => System.Math.Sqrt(x * x + y * y);

        /// <summary>
        /// Return e raised to the power x, minus 1. More accurate than exp(x) - 1 for small x.
        /// </summary>
        public static double Expm1(double x)
        {
            // For small x, use Taylor series for better accuracy
            if (System.Math.Abs(x) < 1e-5)
            {
                return x + 0.5 * x * x + x * x * x / 6.0;
            }

            return System.Math.Exp(x) - 1.0;
        }

        /// <summary>
        /// Return the natural logarithm of 1+x (base e). More accurate than log(1+x) for small x.
        /// </summary>
        public static double Log1p(double x)
        {
            // For small x, use the series expansion for better accuracy
            if (System.Math.Abs(x) < 1e-4)
            {
                double x2 = x * x;
                return x - x2 / 2.0 + x2 * x / 3.0 - x2 * x2 / 4.0;
            }

            return System.Math.Log(1.0 + x);
        }

        /// <summary>
        /// Return the IEEE 754-style remainder of x with respect to y.
        /// </summary>
        public static double Remainder(double x, double y) => System.Math.IEEERemainder(x, y);

        /// <summary>
        /// Return the factorial of n. Raises ValueError if n is negative or OverflowException if n is too large (n > 20).
        /// </summary>
        public static long Factorial(int n)
        {
            if (n < 0)
            {
                throw new ValueError("factorial() not defined for negative values");
            }

            if (n > 20)
            {
                throw new OverflowException("factorial() result too large for long type (n > 20)");
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
}
