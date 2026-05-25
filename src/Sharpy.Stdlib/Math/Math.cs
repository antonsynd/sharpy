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
    public static partial class MathModule
    {
        public static double Pi = 3.141592653589793d;
        public static double E = 2.718281828459045d;
        public static double Tau = 6.283185307179586d;
        public static double Inf = global::System.Double.PositiveInfinity;
        public static double Nan = global::System.Double.NaN;
        public static double Ceil(double x)
        {
            return global::System.Math.Ceiling(x);
        }

        public static double Floor(double x)
        {
            return global::System.Math.Floor(x);
        }

        public static double Fabs(double x)
        {
            return global::System.Math.Abs(x);
        }

        public static double Copysign(double x, double y)
        {
            double sign = (y < 0.0d || (y == 0.0d && 1.0d / y < 0.0d)) ? -1.0d : 1.0d;
            return global::System.Math.Abs(x) * sign;
        }

        public static double Sqrt(double x)
        {
            return global::System.Math.Sqrt(x);
        }

        public static double Pow(double x, double y)
        {
            return global::System.Math.Pow(x, y);
        }

        public static double Exp(double x)
        {
            return global::System.Math.Exp(x);
        }

        public static double Log(double x)
        {
            return global::System.Math.Log(x);
        }

        public static double Log10(double x)
        {
            return global::System.Math.Log10(x);
        }

        public static double Log2(double x)
        {
            return global::System.Math.Log(x, 2.0d);
        }

        public static double Sin(double x)
        {
            return global::System.Math.Sin(x);
        }

        public static double Cos(double x)
        {
            return global::System.Math.Cos(x);
        }

        public static double Tan(double x)
        {
            return global::System.Math.Tan(x);
        }

        public static double Asin(double x)
        {
            return global::System.Math.Asin(x);
        }

        public static double Acos(double x)
        {
            return global::System.Math.Acos(x);
        }

        public static double Atan(double x)
        {
            return global::System.Math.Atan(x);
        }

        public static double Atan2(double y, double x)
        {
            return global::System.Math.Atan2(y, x);
        }

        public static double Sinh(double x)
        {
            return global::System.Math.Sinh(x);
        }

        public static double Cosh(double x)
        {
            return global::System.Math.Cosh(x);
        }

        public static double Tanh(double x)
        {
            return global::System.Math.Tanh(x);
        }

        public static double Degrees(double x)
        {
            return x * (180.0d / Pi);
        }

        public static double Radians(double x)
        {
            return x * (Pi / 180.0d);
        }

        public static bool Isfinite(double x)
        {
            return !global::System.Double.IsInfinity(x) && !global::System.Double.IsNaN(x);
        }

        public static bool Isinf(double x)
        {
            return global::System.Double.IsInfinity(x);
        }

        public static bool Isnan(double x)
        {
            return global::System.Double.IsNaN(x);
        }

        public static double Trunc(double x)
        {
            return global::System.Math.Truncate(x);
        }

        public static double Expm1(double x)
        {
            if (global::System.Math.Abs(x) < 1e-5d)
            {
                return x + 0.5d * x * x + x * x * x / 6.0d;
            }

            return global::System.Math.Exp(x) - 1.0d;
        }

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

        public static double Remainder(double x, double y)
        {
            if (y == 0.0d)
            {
                throw new global::Sharpy.ValueError("math domain error");
            }

            return global::System.Math.IEEERemainder(x, y);
        }

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

        public static double Hypot(double x, double y)
        {
            return global::System.Math.Sqrt(x * x + y * y);
        }
    }
}
