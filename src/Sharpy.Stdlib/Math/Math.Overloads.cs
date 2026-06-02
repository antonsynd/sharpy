using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Mathematical functions (equivalent to Python's math module).</summary>
    public static partial class MathModule
    {
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
