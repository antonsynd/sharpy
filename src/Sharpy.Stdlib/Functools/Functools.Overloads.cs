using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Higher-order functions and operations on callable objects.</summary>
    public static partial class Functools
    {
        /// <summary>Apply function of two arguments cumulatively to the items of iterable, with an initial value.</summary>
        public static T Reduce<T>(Func<T, T, T> func, IEnumerable<T> iterable, T initial)
        {
            T accumulator = initial;
            foreach (T item in iterable)
            {
                accumulator = func(accumulator, item);
            }

            return accumulator;
        }

    }
}
