// Generated from src/Sharpy.Stdlib/spy/functools.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/functools.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Higher-order functions and operations on callable objects.
    /// </summary>
    public static partial class Functools
    {
        /// <summary>
        /// Apply function of two arguments cumulatively to the items of iterable, so as to reduce the iterable to a single value.
        /// </summary>
        public static T Reduce<T>(global::System.Func<T, T, T> func, Sharpy.List<T> iterable)
        {
            Sharpy.List<T> items = new global::Sharpy.List<T>(iterable);
            if (global::Sharpy.Builtins.Len(items) == 0)
            {
                throw new global::Sharpy.TypeError("reduce() of empty iterable with no initial value");
            }

            T accumulator = items[0];
            int i = 1;
            while (i < global::Sharpy.Builtins.Len(items))
            {
                accumulator = func(accumulator, items[i]);
                i = i + 1;
            }

            return accumulator;
        }

        /// <summary>
        /// Apply function of two arguments cumulatively to the items of iterable, starting with initial value.
        /// </summary>
        public static T Reduce<T>(global::System.Func<T, T, T> func, Sharpy.List<T> iterable, T initial)
        {
            T accumulator = initial;
            Sharpy.List<T> items = new global::Sharpy.List<T>(iterable);
            int i = 0;
            while (i < global::Sharpy.Builtins.Len(items))
            {
                accumulator = func(accumulator, items[i]);
                i = i + 1;
            }

            return accumulator;
        }

        /// <summary>
        /// Convert a comparison function into a key function for sorting.
        /// The comparison function should return a negative number for less-than,
        /// zero for equality, or a positive number for greater-than.
        /// </summary>
        public static global::System.Collections.Generic.IComparer<T> CmpToKey<T>(global::System.Func<T, T, int> cmp)
        {
            return Comparer<T>.Create((a, b) => cmp(a, b));
        }
    }
}
