// Generated from src/Sharpy.Stdlib/spy/math_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/math_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Provides mathematical functions and constants, equivalent to Python's math module.
    /// </summary>
    public static partial class MathModule
    {
        /// <summary>The mathematical constant pi = 3.141592653589793.</summary>
        public static double Pi = 3.141592653589793d;
        /// <summary>The mathematical constant e = 2.718281828459045.</summary>
        public static double E = 2.718281828459045d;
        /// <summary>The mathematical constant tau = 2*pi = 6.283185307179586.</summary>
        public static double Tau = 6.283185307179586d;
        /// <summary>A floating-point positive infinity.</summary>
        public static double Inf = global::System.Double.PositiveInfinity;
        /// <summary>A floating-point "not a number" (NaN) value.</summary>
        public static double Nan = global::System.Double.NaN;
        /// <summary>
        /// Return the ceiling of x as a float.
        /// </summary>
        public static double Ceil(double x)
        {
            return global::System.Math.Ceiling(x);
        }

        /// <summary>
        /// Return the floor of x as a float.
        /// </summary>
        public static double Floor(double x)
        {
            return global::System.Math.Floor(x);
        }

        /// <summary>
        /// Return the absolute value of the float x.
        /// </summary>
        public static double Fabs(double x)
        {
            return global::System.Math.Abs(x);
        }

        /// <summary>
        /// Return a float with the magnitude of x but the sign of y.
        /// </summary>
        public static double Copysign(double x, double y)
        {
            double sign = (y < 0.0d || (y == 0.0d && 1.0d / y < 0.0d)) ? -1.0d : 1.0d;
            return global::System.Math.Abs(x) * sign;
        }

        /// <summary>
        /// Return the square root of x.
        /// </summary>
        public static double Sqrt(double x)
        {
            return global::System.Math.Sqrt(x);
        }

        /// <summary>
        /// Return x raised to the power y.
        /// </summary>
        public static double Pow(double x, double y)
        {
            return global::System.Math.Pow(x, y);
        }

        /// <summary>
        /// Return e raised to the power of x.
        /// </summary>
        public static double Exp(double x)
        {
            return global::System.Math.Exp(x);
        }

        /// <summary>
        /// Return the natural logarithm of x (base e).
        /// </summary>
        public static double Log(double x)
        {
            return global::System.Math.Log(x);
        }

        /// <summary>
        /// Return the base 10 logarithm of x.
        /// </summary>
        public static double Log10(double x)
        {
            return global::System.Math.Log10(x);
        }

        /// <summary>
        /// Return the base 2 logarithm of x.
        /// </summary>
        public static double Log2(double x)
        {
            return global::System.Math.Log(x, 2.0d);
        }

        /// <summary>
        /// Return the sine of x (measured in radians).
        /// </summary>
        public static double Sin(double x)
        {
            return global::System.Math.Sin(x);
        }

        /// <summary>
        /// Return the cosine of x (measured in radians).
        /// </summary>
        public static double Cos(double x)
        {
            return global::System.Math.Cos(x);
        }

        /// <summary>
        /// Return the tangent of x (measured in radians).
        /// </summary>
        public static double Tan(double x)
        {
            return global::System.Math.Tan(x);
        }

        /// <summary>
        /// Return the arc sine (measured in radians) of x.
        /// </summary>
        public static double Asin(double x)
        {
            return global::System.Math.Asin(x);
        }

        /// <summary>
        /// Return the arc cosine (measured in radians) of x.
        /// </summary>
        public static double Acos(double x)
        {
            return global::System.Math.Acos(x);
        }

        /// <summary>
        /// Return the arc tangent (measured in radians) of x.
        /// </summary>
        public static double Atan(double x)
        {
            return global::System.Math.Atan(x);
        }

        /// <summary>
        /// Return the arc tangent (measured in radians) of y/x.
        /// </summary>
        public static double Atan2(double y, double x)
        {
            return global::System.Math.Atan2(y, x);
        }

        /// <summary>
        /// Return the hyperbolic sine of x.
        /// </summary>
        public static double Sinh(double x)
        {
            return global::System.Math.Sinh(x);
        }

        /// <summary>
        /// Return the hyperbolic cosine of x.
        /// </summary>
        public static double Cosh(double x)
        {
            return global::System.Math.Cosh(x);
        }

        /// <summary>
        /// Return the hyperbolic tangent of x.
        /// </summary>
        public static double Tanh(double x)
        {
            return global::System.Math.Tanh(x);
        }

        /// <summary>
        /// Convert angle x from radians to degrees.
        /// </summary>
        public static double Degrees(double x)
        {
            return x * (180.0d / Pi);
        }

        /// <summary>
        /// Convert angle x from degrees to radians.
        /// </summary>
        public static double Radians(double x)
        {
            return x * (Pi / 180.0d);
        }

        /// <summary>
        /// Return True if x is neither an infinity nor a NaN, and False otherwise.
        /// </summary>
        public static bool Isfinite(double x)
        {
            return !global::System.Double.IsInfinity(x) && !global::System.Double.IsNaN(x);
        }

        /// <summary>
        /// Return True if x is a positive or negative infinity, and False otherwise.
        /// </summary>
        public static bool Isinf(double x)
        {
            return global::System.Double.IsInfinity(x);
        }

        /// <summary>
        /// Return True if x is a NaN (not a number), and False otherwise.
        /// </summary>
        public static bool Isnan(double x)
        {
            return global::System.Double.IsNaN(x);
        }

        /// <summary>
        /// Truncate x to the nearest integral value toward 0.
        /// </summary>
        public static double Trunc(double x)
        {
            return global::System.Math.Truncate(x);
        }

        /// <summary>
        /// Return exp(x) - 1, computed in a way that is accurate for small x.
        /// </summary>
        public static double Expm1(double x)
        {
            if (global::System.Math.Abs(x) < 1e-5d)
            {
                return x + 0.5d * x * x + x * x * x / 6.0d;
            }

            return global::System.Math.Exp(x) - 1.0d;
        }

        /// <summary>
        /// Return the natural logarithm of 1+x (base e), computed in a way that is accurate for small x.
        /// </summary>
        public static double Log1p(double x)
        {
            if (x <= -1.0d)
            {
                throw new global::Sharpy.ValueError("math domain error");
            }

            if (global::System.Math.Abs(x) < 1e-4d)
            {
                double x2 = x * x;
                return x - x2 / 2.0d + x2 * x / 3.0d - x2 * x2 / 4.0d;
            }

            return global::System.Math.Log(1.0d + x);
        }

        /// <summary>
        /// Return the IEEE 754-style remainder of x with respect to y.
        /// </summary>
        public static double Remainder(double x, double y)
        {
            if (y == 0.0d)
            {
                throw new global::Sharpy.ValueError("math domain error");
            }

            return global::System.Math.IEEERemainder(x, y);
        }

        /// <summary>
        /// Return the greatest common divisor of a and b.
        /// </summary>
        public static long Gcd(long a, long b)
        {
            a = global::System.Math.Abs(a);
            b = global::System.Math.Abs(b);
            while (b != 0)
            {
                long temp = b;
                b = a % b;
                a = temp;
            }

            return a;
        }

        /// <summary>
        /// Return n factorial. Raises ValueError for negative n and OverflowError for n > 20.
        /// </summary>
        public static long Factorial(int n)
        {
            if (n < 0)
            {
                throw new global::Sharpy.ValueError("factorial() not defined for negative values");
            }

            if (n > 20)
            {
                throw new global::Sharpy.OverflowError("factorial() result too large for long type (n > 20)");
            }

            if (n == 0 || n == 1)
            {
                return 1;
            }

            long result = 1;
            int i = 2;
            while (i <= n)
            {
                result = result * i;
                i = i + 1;
            }

            return result;
        }

        /// <summary>
        /// Determine whether two floating-point numbers are close in value.
        /// </summary>
        public static bool Isclose(double a, double b, double relTol = 1e-9d, double absTol = 0.0d)
        {
            if (relTol < 0.0d || absTol < 0.0d)
            {
                throw new global::Sharpy.ValueError("tolerances must be non-negative");
            }

            if (a == b)
            {
                return true;
            }

            if (global::System.Double.IsInfinity(a) || global::System.Double.IsInfinity(b))
            {
                return false;
            }

            if (global::System.Double.IsNaN(a) || global::System.Double.IsNaN(b))
            {
                return false;
            }

            double diff = global::System.Math.Abs(b - a);
            return diff <= global::System.Math.Max(relTol * global::System.Math.Max(global::System.Math.Abs(a), global::System.Math.Abs(b)), absTol);
        }

        /// <summary>
        /// Return the number of ways to choose k items from n items without repetition and with order.
        /// </summary>
        public static long Perm(int n, int k)
        {
            if (n < 0)
            {
                throw new global::Sharpy.ValueError("n must not be negative");
            }

            if (k < 0)
            {
                throw new global::Sharpy.ValueError("k must not be negative");
            }

            if (k > n)
            {
                return 0;
            }

            long result = 1;
            int i = 0;
            while (i < k)
            {
                result = result * (n - i);
                i = i + 1;
            }

            return result;
        }

        /// <summary>
        /// Return an accurate floating-point sum of values in the iterable.
        /// </summary>
        public static double Fsum(Sharpy.List<double> iterable)
        {
            double total = 0.0d;
            double c = 0.0d;
            foreach (var __loopVar_0 in iterable)
            {
                var value = __loopVar_0;
                double y = value - c;
                double t = total + y;
                c = (t - total) - y;
                total = t;
            }

            return total;
        }

        /// <summary>
        /// Return the product of all the elements in the iterable.
        /// </summary>
        public static double Prod(Sharpy.List<double> iterable, double start = 1.0d)
        {
            double result = start;
            foreach (var __loopVar_1 in iterable)
            {
                var value = __loopVar_1;
                result = result * value;
            }

            return result;
        }

        /// <summary>
        /// Return the Euclidean distance, sqrt(x*x + y*y).
        /// </summary>
        public static double Hypot(double x, double y)
        {
            return global::System.Math.Sqrt(x * x + y * y);
        }
    }
}
