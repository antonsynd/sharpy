using System.Collections.Generic;
using System;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a new sorted list from the items in iterable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <param name="iterable">The iterable to sort</param>
        /// <returns>A new sorted list</returns>
        /// <example>
        /// <code>
        /// sorted([3, 1, 2])              # [1, 2, 3]
        /// sorted("cab")                  # ["a", "b", "c"]
        /// sorted([3, 1, 2], reverse=True) # [3, 2, 1]
        /// </code>
        /// </example>
        public static List<T> Sorted<T>(IEnumerable<T> iterable)
        {
            return SortedImpl(iterable, ComparerAdapter<T>.Instance, reverse: false);
        }

        /// <summary>
        /// Return a new sorted list using a key function for comparison.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <typeparam name="TKey">The type of the comparison key</typeparam>
        /// <param name="iterable">The iterable to sort</param>
        /// <param name="key">A function to extract a comparison key from each element</param>
        /// <returns>A new sorted list</returns>
        public static List<T> Sorted<T, TKey>(IEnumerable<T> iterable, Func<T, TKey> key)
        {
            if (key is null)
            {
                throw TypeError.ArgNone("sorted", "key");
            }
            return SortedImpl(iterable, KeyComparerFactory<T, TKey>.Create(key), reverse: false);
        }

        /// <summary>
        /// Return a new sorted list, optionally in reverse order.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <param name="iterable">The iterable to sort</param>
        /// <param name="reverse">If True, sort in descending order</param>
        /// <returns>A new sorted list</returns>
        public static List<T> Sorted<T>(IEnumerable<T> iterable, bool reverse)
        {
            return SortedImpl(iterable, ComparerAdapter<T>.Instance, reverse);
        }

        /// <summary>
        /// Return a new sorted list using a key function, optionally in reverse order.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <typeparam name="TKey">The type of the comparison key</typeparam>
        /// <param name="iterable">The iterable to sort</param>
        /// <param name="key">A function to extract a comparison key from each element</param>
        /// <param name="reverse">If True, sort in descending order</param>
        /// <returns>A new sorted list</returns>
        public static List<T> Sorted<T, TKey>(IEnumerable<T> iterable, Func<T, TKey> key, bool reverse)
        {
            if (key is null)
            {
                throw TypeError.ArgNone("sorted", "key");
            }
            return SortedImpl(iterable, KeyComparerFactory<T, TKey>.Create(key), reverse);
        }

        private static List<T> SortedImpl<T>(IEnumerable<T> iterable, IComparer<T> comparer, bool reverse)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sorted", "iterable");
            }

            // Python's sort is stable, and `reverse` sorts descending while PRESERVING the
            // original order among equal elements — so it has to be handled inside the merge,
            // not by reversing afterwards. List.StableSort is the same engine list.sort() uses,
            // which is what keeps the two surfaces in agreement (#1191).
            var result = new List<T>(iterable);
            result.StableSort(comparer, reverse);
            return result;
        }
    }
}
