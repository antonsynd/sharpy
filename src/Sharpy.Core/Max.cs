using System.Collections.Generic;
using System;
namespace Sharpy
{

    public static partial class Builtins
    {
        /// <summary>
        /// Return the largest item in an iterable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <param name="iterable">The iterable to search</param>
        /// <returns>The largest item</returns>
        /// <exception cref="ValueError">Thrown when the iterable is empty</exception>
        /// <example>
        /// <code>
        /// max([1, 5, 3])       # 5
        /// max("abc")           # "c"
        /// </code>
        /// </example>
        public static T Max<T>(IEnumerable<T> iterable)
        {
            return Max(iterable, value => value);
        }

        /// <summary>
        /// Return the largest item in an iterable, using a key function for comparison.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <typeparam name="TKey">The type of the key used for comparison</typeparam>
        /// <param name="iterable">The iterable to search</param>
        /// <param name="key">A function to extract a comparison key from each element</param>
        /// <returns>The largest item according to the key function</returns>
        /// <exception cref="ValueError">Thrown when the iterable is empty</exception>
        public static T Max<T, TKey>(IEnumerable<T> iterable, Func<T, TKey> key)
        {
            if (iterable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            if (key is null)
            {
                throw TypeError.ArgNone("max", "key");
            }

            bool iterableIsEmpty = true;
            T? biggest = default;

            foreach (var elem in iterable)
            {
                if (elem is null)
                {
                    throw TypeError.OpNotSupported("<", "NoneType");
                }

                if (biggest is null || iterableIsEmpty)
                {
                    biggest = elem;
                    iterableIsEmpty = false;

                    continue;
                }

                if (Operator.Lt(key(biggest), key(elem)))
                {
                    biggest = elem;
                }
            }

            if (biggest is null || iterableIsEmpty)
            {
                throw new ValueError("max() arg is an empty sequence");
            }

            return biggest;
        }

        /// <summary>
        /// Return the largest item in an iterable, or default if the iterable is empty.
        /// </summary>
        public static T Max<T>(IEnumerable<T> iterable, T @default)
        {
            return Max(iterable, value => value, @default);
        }

        /// <summary>
        /// Return the largest item in an iterable using a key function,
        /// or default if the iterable is empty.
        /// </summary>
        public static T Max<T, TKey>(IEnumerable<T> iterable, Func<T, TKey> key, T @default)
        {
            if (iterable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            if (key is null)
            {
                throw TypeError.ArgNone("max", "key");
            }

            bool iterableIsEmpty = true;
            T? biggest = default;

            foreach (var elem in iterable)
            {
                if (elem is null)
                {
                    throw TypeError.OpNotSupported("<", "NoneType");
                }

                if (biggest is null || iterableIsEmpty)
                {
                    biggest = elem;
                    iterableIsEmpty = false;
                    continue;
                }

                if (Operator.Lt(key(biggest), key(elem)))
                {
                    biggest = elem;
                }
            }

            if (biggest is null || iterableIsEmpty)
            {
                return @default;
            }

            return biggest;
        }

        /// <summary>
        /// Return the largest of two or more values (the variadic value form).
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="first">The first value</param>
        /// <param name="second">The second value</param>
        /// <param name="rest">Any additional values</param>
        /// <returns>The largest value (the first encountered on ties, matching Python)</returns>
        /// <remarks>
        /// The <c>key=</c> form of the variadic value call (e.g. <c>max(a, b, key=f)</c>) is not
        /// supported yet: in C# a <c>params</c> parameter must come last, so a key function cannot
        /// be passed by keyword alongside positional values. Tracked by #1012.
        /// </remarks>
        /// <example>
        /// <code>
        /// max(2, 3, 1)     # 3
        /// max(5, 2, 8, 1)  # 8
        /// </code>
        /// </example>
        public static T Max<T>(T first, T second, params T[] rest)
        {
            if (first is null || second is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            // Tie-break to the first occurrence (matching Python): only replace on strictly-greater.
            T biggest = Operator.Lt(first, second) ? second : first;

            foreach (var elem in rest)
            {
                if (elem is null)
                {
                    throw TypeError.OpNotSupported("<", "NoneType");
                }

                if (Operator.Lt(biggest, elem))
                {
                    biggest = elem;
                }
            }

            return biggest;
        }
    }
}
