using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class MathModule
    {
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

        public static long Perm(int n)
        {
            return Factorial(n);
        }

        public static long Prod(IEnumerable<int> iterable, long start = 1)
        {
            long result = start;
            foreach (int value in iterable)
            {
                result *= value;
            }

            return result;
        }

        public static double Log(double x, double baseValue)
        {
            return System.Math.Log(x, baseValue);
        }
    }
}
