using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class MathModule
    {
        /// <summary>Return the least common multiple of a and b.</summary>
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

        /// <summary>Return the number of ways to choose k items from n items without repetition and without order.</summary>
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

        /// <summary>Return the number of permutations of n items, equivalent to n factorial.</summary>
        public static long Perm(int n)
        {
            return Factorial(n);
        }

        /// <summary>Return the product of all the elements in the iterable, starting with the given start value.</summary>
        public static long Prod(IEnumerable<int> iterable, long start = 1)
        {
            long result = start;
            foreach (int value in iterable)
            {
                result *= value;
            }

            return result;
        }

        /// <summary>Return the logarithm of x to the given base.</summary>
        public static double Log(double x, double baseValue)
        {
            return System.Math.Log(x, baseValue);
        }
    }
}
