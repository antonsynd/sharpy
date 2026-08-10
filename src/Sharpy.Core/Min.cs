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

            // The incumbent's key is CARRIED, not recomputed (#1416). Recomputing it on every
            // comparison cost 2(N-1) key evaluations against CPython's N, and skipping the first
            // element's key entirely cost 0 against CPython's 1 for a single-element sequence. A key
            // may log, count or memoise, so the evaluation count is observable semantics, not just
            // a cost — and it is a cost too, since the key is the expensive part of a keyed min.
            bool iterableIsEmpty = true;
            T? smallest = default;
            TKey? smallestKey = default;

            foreach (var elem in iterable)
            {
                if (elem is null)
                {
                    throw TypeError.OpNotSupported("<", "NoneType");
                }

                // Exactly once per element, the first included.
                TKey elemKey = key(elem);

                if (iterableIsEmpty)
                {
                    smallest = elem;
                    smallestKey = elemKey;
                    iterableIsEmpty = false;

                    continue;
                }

                if (Operator.Lt(elemKey, smallestKey!))
                {
                    smallest = elem;
                    smallestKey = elemKey;
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

            // Carries the incumbent's key, exactly as the two-argument overload does (#1416). This
            // overload holds its own copy of the loop, so it held its own copy of the defect.
            bool iterableIsEmpty = true;
            T? smallest = default;
            TKey? smallestKey = default;

            foreach (var elem in iterable)
            {
                if (elem is null)
                {
                    throw TypeError.OpNotSupported("<", "NoneType");
                }

                TKey elemKey = key(elem);

                if (iterableIsEmpty)
                {
                    smallest = elem;
                    smallestKey = elemKey;
                    iterableIsEmpty = false;
                    continue;
                }

                if (Operator.Lt(elemKey, smallestKey!))
                {
                    smallest = elem;
                    smallestKey = elemKey;
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
        /// The <c>key=</c> form of this variadic value call (e.g. <c>min(a, b, key=f)</c>) is
        /// supported: the compiler lowers it to the iterable+key overload
        /// <c>Min&lt;T, TKey&gt;(IEnumerable&lt;T&gt;, Func&lt;T, TKey&gt;)</c> by wrapping the
        /// positional values in an array, because a C# <c>params</c> parameter must come last and
        /// cannot coexist with a by-keyword <c>key</c> (#1012).
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
