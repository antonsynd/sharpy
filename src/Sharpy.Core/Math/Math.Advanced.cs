using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Math
    {
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
            // Use a / gcd * b to avoid overflow
            return a / Gcd(a, b) * b;
        }

        /// <summary>
        /// Return True if the values a and b are close to each other, and False otherwise.
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

            if (double.IsNaN(a) || double.IsNaN(b))
            {
                return false;
            }

            double diff = System.Math.Abs(b - a);
            return diff <= System.Math.Max(rel_tol * System.Math.Max(System.Math.Abs(a), System.Math.Abs(b)), abs_tol);
        }

        /// <summary>
        /// Return the number of ways to choose k items from n items without repetition and without order.
        /// </summary>
        public static long Comb(int n, int k)
        {
            if (n < 0)
            {
                throw new ValueError("n must not be negative");
            }

            if (k < 0)
            {
                throw new ValueError("k must not be negative");
            }

            if (k > n)
            {
                return 0;
            }

            // Use symmetry: C(n, k) == C(n, n-k)
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
        /// If k is not specified, then k defaults to n and the function returns n!.
        /// </summary>
        public static long Perm(int n, int k)
        {
            if (n < 0)
            {
                throw new ValueError("n must not be negative");
            }

            if (k < 0)
            {
                throw new ValueError("k must not be negative");
            }

            if (k > n)
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
        /// Return the number of ways to arrange n items (n!).
        /// </summary>
        public static long Perm(int n)
        {
            return Factorial(n);
        }

        /// <summary>
        /// Return an accurate floating point sum of values in the iterable, using Kahan summation.
        /// </summary>
        public static double Fsum(IEnumerable<double> iterable)
        {
            double sum = 0.0;
            double c = 0.0; // compensation for lost low-order bits
            foreach (double value in iterable)
            {
                double y = value - c;
                double t = sum + y;
                c = (t - sum) - y;
                sum = t;
            }

            return sum;
        }

        /// <summary>
        /// Return the product of a start value (default: 1) times an iterable of numbers.
        /// </summary>
        public static double Prod(IEnumerable<double> iterable, double start = 1.0)
        {
            double result = start;
            foreach (double value in iterable)
            {
                result *= value;
            }

            return result;
        }

        /// <summary>
        /// Return the product of a start value (default: 1) times an iterable of integers.
        /// </summary>
        public static long Prod(IEnumerable<int> iterable, long start = 1)
        {
            long result = start;
            foreach (int value in iterable)
            {
                result *= value;
            }

            return result;
        }

        /// <summary>
        /// Return the Euclidean distance, sqrt(x*x + y*y).
        /// </summary>
        public static double Hypot(double x, double y)
        {
            return System.Math.Sqrt(x * x + y * y);
        }
    }
}
