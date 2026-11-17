// Generated from src/Sharpy.Stdlib/spy/math_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/math_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy.MathModule
{
    /// <summary>
    /// Mathematical functions (trigonometric, logarithmic, etc.).
    /// </summary>
    public static partial class MathModuleModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double Pi__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Pi__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Pi__init()
        {
            Pi__field = 3.141592653589793d;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Pi__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Pi__state, "pi", Pi__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref double Pi
        {
            get
            {
                if (!__Swept.Done)
                    Pi__slow();
                return ref Pi__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Pi__ensure()
        {
            if (!__Swept.Done)
                Pi__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double Pi__set(double value)
        {
            if (__Swept.Done)
                Pi__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Pi__state, ref Pi__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<double>? Pi__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Pi__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<double>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref double Pi__out(out global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            spill = Pi__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Pi__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Pi__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            if (spill != null)
                Pi__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double E__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte E__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void E__init()
        {
            E__field = 2.718281828459045d;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void E__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref E__state, "e", E__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref double E
        {
            get
            {
                if (!__Swept.Done)
                    E__slow();
                return ref E__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void E__ensure()
        {
            if (!__Swept.Done)
                E__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double E__set(double value)
        {
            if (__Swept.Done)
                E__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref E__state, ref E__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<double>? E__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref E__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<double>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref double E__out(out global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            spill = E__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref E__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R E__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            if (spill != null)
                E__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double Tau__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Tau__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Tau__init()
        {
            Tau__field = 6.283185307179586d;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Tau__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Tau__state, "tau", Tau__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref double Tau
        {
            get
            {
                if (!__Swept.Done)
                    Tau__slow();
                return ref Tau__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Tau__ensure()
        {
            if (!__Swept.Done)
                Tau__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double Tau__set(double value)
        {
            if (__Swept.Done)
                Tau__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Tau__state, ref Tau__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<double>? Tau__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Tau__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<double>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref double Tau__out(out global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            spill = Tau__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Tau__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Tau__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            if (spill != null)
                Tau__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double Inf__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Inf__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Inf__init()
        {
            Inf__field = global::System.Double.PositiveInfinity;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Inf__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Inf__state, "inf", Inf__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref double Inf
        {
            get
            {
                if (!__Swept.Done)
                    Inf__slow();
                return ref Inf__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Inf__ensure()
        {
            if (!__Swept.Done)
                Inf__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double Inf__set(double value)
        {
            if (__Swept.Done)
                Inf__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Inf__state, ref Inf__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<double>? Inf__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Inf__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<double>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref double Inf__out(out global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            spill = Inf__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Inf__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Inf__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            if (spill != null)
                Inf__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double Nan__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Nan__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Nan__init()
        {
            Nan__field = global::System.Double.NaN;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Nan__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Nan__state, "nan", Nan__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref double Nan
        {
            get
            {
                if (!__Swept.Done)
                    Nan__slow();
                return ref Nan__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Nan__ensure()
        {
            if (!__Swept.Done)
                Nan__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double Nan__set(double value)
        {
            if (__Swept.Done)
                Nan__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Nan__state, ref Nan__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<double>? Nan__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Nan__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<double>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref double Nan__out(out global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            spill = Nan__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Nan__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Nan__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<double>? spill)
        {
            if (spill != null)
                Nan__set(spill.Value!);
            return result;
        }

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
            double sign = global::System.Double.IsNegative(y) ? -1.0d : 1.0d;
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
        /// Return the logarithm of x to the given base.
        /// </summary>
        public static double Log(double x, double @base)
        {
            return global::System.Math.Log(x, @base);
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
            return x * (global::Sharpy.Builtins.TrueDiv(180.0d, global::Sharpy.MathModule.MathModuleModule.Pi));
        }

        /// <summary>
        /// Convert angle x from degrees to radians.
        /// </summary>
        public static double Radians(double x)
        {
            return x * (global::Sharpy.MathModule.MathModuleModule.Pi / 180.0d);
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
                b = global::Sharpy.Builtins.FloorMod(a, b);
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
        /// Return the number of permutations of n items, equivalent to n factorial.
        /// </summary>
        public static long Perm(int n)
        {
            return global::Sharpy.MathModule.MathModuleModule.Factorial(n);
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
        /// Return the least common multiple of a and b.
        /// </summary>
        public static long Lcm(long a, long b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }

            a = global::System.Math.Abs(a);
            b = global::System.Math.Abs(b);
            return global::Sharpy.Builtins.FloorDiv(a, global::Sharpy.MathModule.MathModuleModule.Gcd(a, b)) * b;
        }

        /// <summary>
        /// Return the number of ways to choose k items from n items without repetition and without order.
        /// </summary>
        public static long Comb(int n, int k)
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

            if (k > n - k)
            {
                k = n - k;
            }

            long result = 1;
            int i = 0;
            while (i < k)
            {
                result = global::Sharpy.Builtins.FloorDiv(result * (n - i), (i + 1));
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
        /// Return the product of all the elements in the iterable, starting with the given start value.
        /// </summary>
        public static long Prod(Sharpy.List<int> iterable, long start = 1)
        {
            long result = start;
            foreach (var __loopVar_2 in iterable)
            {
                var value = __loopVar_2;
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

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref Pi__state, "pi", Pi__init);
            global::Sharpy.StaticInit.Run(ref E__state, "e", E__init);
            global::Sharpy.StaticInit.Run(ref Tau__state, "tau", Tau__init);
            global::Sharpy.StaticInit.Run(ref Inf__state, "inf", Inf__init);
            global::Sharpy.StaticInit.Run(ref Nan__state, "nan", Nan__init);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static class __Swept
        {
            internal static readonly bool Done;
            static __Swept()
            {
                Done = global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep);
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        [global::Sharpy.SharpyLoadEntry(true)]
        public static void __Load()
        {
            if (!__Swept.Done)
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
        }
    }
}
