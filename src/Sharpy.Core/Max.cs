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
    }
}
