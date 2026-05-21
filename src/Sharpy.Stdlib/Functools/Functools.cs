using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Higher-order functions and operations on callable objects, similar to
    /// Python's functools module.
    /// </summary>
    public static partial class Functools
    {
        /// <summary>
        /// Apply a function of two arguments cumulatively to the items of
        /// <paramref name="iterable"/>, from left to right, so as to reduce
        /// the iterable to a single value.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="func">A function of two arguments.</param>
        /// <param name="iterable">The iterable to reduce.</param>
        /// <returns>The single result of the cumulative application.</returns>
        /// <exception cref="TypeError">Thrown if the iterable is empty.</exception>
        /// <example>
        /// <code>
        /// functools.reduce(lambda x, y: x + y, [1, 2, 3, 4, 5])    # 15
        /// </code>
        /// </example>
        public static T Reduce<T>(Func<T, T, T> func, IEnumerable<T> iterable)
        {
            using (var enumerator = iterable.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    throw new TypeError("reduce() of empty iterable with no initial value");
                }

                T accumulator = enumerator.Current;
                while (enumerator.MoveNext())
                {
                    accumulator = func(accumulator, enumerator.Current);
                }

                return accumulator;
            }
        }

        /// <summary>
        /// Apply a function of two arguments cumulatively to the items of
        /// <paramref name="iterable"/>, from left to right, starting with
        /// <paramref name="initial"/>, so as to reduce the iterable to a
        /// single value.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="func">A function of two arguments.</param>
        /// <param name="iterable">The iterable to reduce.</param>
        /// <param name="initial">The initial (seed) value.</param>
        /// <returns>The single result of the cumulative application.</returns>
        /// <example>
        /// <code>
        /// functools.reduce(lambda x, y: x + y, [1, 2, 3, 4, 5], 10)    # 25
        /// </code>
        /// </example>
        public static T Reduce<T>(Func<T, T, T> func, IEnumerable<T> iterable, T initial)
        {
            T accumulator = initial;
            foreach (T item in iterable)
            {
                accumulator = func(accumulator, item);
            }

            return accumulator;
        }

        /// <summary>
        /// Convert a comparison function into a key function suitable for use
        /// with sorting. Returns an <see cref="IComparer{T}"/> that wraps the
        /// comparison function.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="func">A comparison function that returns a negative number
        /// for less-than, zero for equal, or a positive number for greater-than.</param>
        /// <returns>An <see cref="IComparer{T}"/> wrapping the comparison function.</returns>
        /// <example>
        /// <code>
        /// comparer = functools.cmp_to_key(lambda a, b: a - b)
        /// sorted([3, 1, 2], key=comparer)    # [1, 2, 3]
        /// </code>
        /// </example>
        public static IComparer<T> CmpToKey<T>(Func<T, T, int> func)
        {
            return new CmpComparer<T>(func);
        }

        private class CmpComparer<T> : IComparer<T>
        {
            private readonly Func<T, T, int> _func;

            public CmpComparer(Func<T, T, int> func)
            {
                _func = func;
            }

            public int Compare(T? x, T? y)
            {
                return _func(x!, y!);
            }
        }
    }
}
