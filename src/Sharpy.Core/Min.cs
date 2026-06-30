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
            }

            if (smallest is null || iterableIsEmpty)
            {
                throw new ValueError("min() arg is an empty sequence");
            }

            return smallest;
        }

        /// <summary>
        /// Return the smallest item in an iterable, or default if the iterable is empty.
        /// </summary>
        public static T Min<T>(IEnumerable<T> iterable, T @default)
        {
            return Min(iterable, value => value, @default);
        }

        /// <summary>
        /// Return the smallest item in an iterable using a key function,
        /// or default if the iterable is empty.
        /// </summary>
        public static T Min<T, TKey>(IEnumerable<T> iterable, Func<T, TKey> key, T @default)
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
            }

            if (smallest is null || iterableIsEmpty)
            {
                return @default;
            }

            return smallest;
        }

        /// <summary>
        /// Return the smallest of two or more values (the variadic value form).
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="first">The first value</param>
        /// <param name="second">The second value</param>
        /// <param name="rest">Any additional values</param>
        /// <returns>The smallest value (the first encountered on ties, matching Python)</returns>
        /// <remarks>
        /// The <c>key=</c> form of the variadic value call (e.g. <c>min(a, b, key=f)</c>) is not
        /// supported yet: in C# a <c>params</c> parameter must come last, so a key function cannot
        /// be passed by keyword alongside positional values. Tracked by #1012.
        /// </remarks>
        /// <example>
        /// <code>
        /// min(2, 3)        # 2
        /// min(5, 2, 8, 1)  # 1
        /// </code>
        /// </example>
        public static T Min<T>(T first, T second, params T[] rest)
        {
            if (first is null || second is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            // Tie-break to the first occurrence (matching Python): only replace on strictly-less.
            T smallest = Operator.Lt(second, first) ? second : first;

            foreach (var elem in rest)
            {
                if (elem is null)
                {
                    throw TypeError.OpNotSupported("<", "NoneType");
                }

                if (Operator.Lt(elem, smallest))
                {
                    smallest = elem;
                }
            }

            return smallest;
        }
    }
}
