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

            // The incumbent's key is CARRIED, not recomputed (#1416) — see Min for the full
            // reasoning; this is its twin, and the two must not drift.
            bool iterableIsEmpty = true;
            T? biggest = default;
            TKey? biggestKey = default;

            foreach (var elem in iterable)
            {
                // Exactly once per element, the first included. A None element is not refused here:
                // python compares keys, and only when a comparison happens (#2085).
                TKey elemKey = key(elem);

                if (iterableIsEmpty)
                {
                    biggest = elem;
                    biggestKey = elemKey;
                    iterableIsEmpty = false;

                    continue;
                }

                // python's order: the new key against the incumbent's, with max's `>` (#2085).
                if (Operator.Gt(elemKey, biggestKey!))
                {
                    biggest = elem;
                    biggestKey = elemKey;
                }
            }

            if (iterableIsEmpty)
            {
                throw new ValueError("max() arg is an empty sequence");
            }

            return biggest!;
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

            // Carries the incumbent's key, exactly as the two-argument overload does (#1416). This
            // overload holds its own copy of the loop, so it held its own copy of the defect.
            bool iterableIsEmpty = true;
            T? biggest = default;
            TKey? biggestKey = default;

            foreach (var elem in iterable)
            {
                TKey elemKey = key(elem);

                if (iterableIsEmpty)
                {
                    biggest = elem;
                    biggestKey = elemKey;
                    iterableIsEmpty = false;
                    continue;
                }

                // python's order: the new key against the incumbent's, with max's `>` (#2085).
                if (Operator.Gt(elemKey, biggestKey!))
                {
                    biggest = elem;
                    biggestKey = elemKey;
                }
            }

            if (iterableIsEmpty)
            {
                return @default;
            }

            return biggest!;
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
        /// The <c>key=</c> form of this variadic value call (e.g. <c>max(a, b, key=f)</c>) is
        /// supported: the compiler lowers it to the iterable+key overload
        /// <c>Max&lt;T, TKey&gt;(IEnumerable&lt;T&gt;, Func&lt;T, TKey&gt;)</c> by wrapping the
        /// positional values in an array, because a C# <c>params</c> parameter must come last and
        /// cannot coexist with a by-keyword <c>key</c> (#1012).
        /// </remarks>
        /// <example>
        /// <code>
        /// max(2, 3, 1)     # 3
        /// max(5, 2, 8, 1)  # 8
        /// </code>
        /// </example>
        public static T Max<T>(T first, T second, params T[] rest)
        {
            // Tie-break to the first occurrence (matching Python): only replace on strictly-greater.
            // Each value is compared against the incumbent as python does, `value > incumbent`, so
            // a refusal names the operands in python's order (#2085).
            T biggest = Operator.Gt(second, first) ? second : first;

            foreach (var elem in rest)
            {
                if (Operator.Gt(elem, biggest))
                {
                    biggest = elem;
                }
            }

            return biggest;
        }
    }
}
