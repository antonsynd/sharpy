using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Mathematical functions (equivalent to Python's math module).</summary>
    public static partial class MathModule
    {
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

    }
}
