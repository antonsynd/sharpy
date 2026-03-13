using System.Collections.Generic;
using System;
namespace Sharpy
{

    public static partial class Builtins
    {
        /// <summary>
        /// Return the smallest item in an iterable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <param name="iterable">The iterable to search</param>
        /// <returns>The smallest item</returns>
        /// <exception cref="ValueError">Thrown when the iterable is empty</exception>
        /// <example>
        /// <code>
        /// min([1, 5, 3])       # 1
        /// min("abc")           # "a"
        /// </code>
        /// </example>
        public static T Min<T>(IEnumerable<T> iterable)
        {
            return Min(iterable, value => value);
        }

        /// <summary>
        /// Return the smallest item in an iterable, using a key function for comparison.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <typeparam name="TKey">The type of the key used for comparison</typeparam>
        /// <param name="iterable">The iterable to search</param>
        /// <param name="key">A function to extract a comparison key from each element</param>
        /// <returns>The smallest item according to the key function</returns>
        /// <exception cref="ValueError">Thrown when the iterable is empty</exception>
        public static T Min<T, TKey>(IEnumerable<T> iterable, Func<T, TKey> key)
        {
            if (iterable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            if (key is null)
            {
                throw TypeError.ArgNone("min", "key");
            }

            bool iterableIsEmpty = true;
            T? smallest = default;

            foreach (var elem in iterable)
            {
                if (elem is null)
                {
                    throw TypeError.OpNotSupported("<", "NoneType");
                }

                if (smallest is null || iterableIsEmpty)
                {
                    smallest = elem;
                    iterableIsEmpty = false;

                    continue;
                }

                if (Operator.Lt(key(elem), key(smallest)))
                {
                    smallest = elem;
                }

                // No-op, these are equivalent, no need to do anything
            }

            if (smallest is null || iterableIsEmpty)
            {
                throw new ValueError("Min() iterable argument is empty");
            }

            return smallest;
        }
    }
}
