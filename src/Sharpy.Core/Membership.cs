using System;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Membership test <c>needle in container</c> for an optional value-type needle: a
        /// <c>None</c> needle equals no element, so the answer is <c>False</c> without asking
        /// the container; otherwise the container answers for the unwrapped value.
        /// </summary>
        /// <remarks>
        /// The needle comes before the container so that a call site evaluates its operands in
        /// Python's order (<c>x</c>, then <c>y</c>, for <c>x in y</c>). The container is a free
        /// type parameter and the test is a delegate so that one helper serves every container
        /// with a <c>Contains</c> method, including one whose parameter the needle only converts
        /// to (an <c>int</c> needle and <c>Contains(long)</c>): the conversion happens in the
        /// caller's lambda. Negation (<c>not in</c>) is the caller's.
        /// A lowering target, not a python builtin: the compiler emits it for <c>in</c> / <c>not in</c>.
        /// </remarks>
        /// <typeparam name="TContainer">The container type</typeparam>
        /// <typeparam name="T">The needle's underlying value type</typeparam>
        /// <param name="needle">The left operand of <c>in</c>; <c>null</c> is <c>None</c></param>
        /// <param name="container">The right operand of <c>in</c></param>
        /// <param name="contains">The container's membership test, e.g. <c>(c, n) => c.Contains(n)</c></param>
        /// <returns><c>false</c> when <paramref name="needle"/> is <c>None</c>; otherwise the
        /// result of <paramref name="contains"/></returns>
        /// <example>
        /// <code>
        /// ContainsOptional((int?)2, xs, static (c, n) => c.Contains(n))     // xs.Contains(2)
        /// ContainsOptional((int?)null, xs, static (c, n) => c.Contains(n))  // false
        /// </code>
        /// </example>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool ContainsOptional<TContainer, T>(T? needle, TContainer container, Func<TContainer, T, bool> contains)
            where T : struct
        {
            return needle.HasValue && contains(container, needle.GetValueOrDefault());
        }

        /// <summary>
        /// Membership test <c>needle in container</c> for an optional value-type needle and a
        /// container that refuses <c>None</c> (a subsequence container such as <c>str</c> or
        /// <c>bytes</c>): a <c>None</c> needle raises <see cref="TypeError"/>, as Python does;
        /// otherwise the container answers for the unwrapped value.
        /// </summary>
        /// <remarks>
        /// The needle comes before the container so that a call site evaluates its operands in
        /// Python's order. Negation (<c>not in</c>) is the caller's.
        /// A lowering target, not a python builtin: the compiler emits it for <c>in</c> / <c>not in</c>.
        /// </remarks>
        /// <typeparam name="TContainer">The container type</typeparam>
        /// <typeparam name="T">The needle's underlying value type</typeparam>
        /// <param name="needle">The left operand of <c>in</c>; <c>null</c> is <c>None</c></param>
        /// <param name="container">The right operand of <c>in</c></param>
        /// <param name="contains">The container's membership test, e.g. <c>(c, n) => c.Contains(n)</c></param>
        /// <returns>The result of <paramref name="contains"/></returns>
        /// <exception cref="TypeError">Thrown when <paramref name="needle"/> is <c>None</c></exception>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool ContainsNoneRaises<TContainer, T>(T? needle, TContainer container, Func<TContainer, T, bool> contains)
            where T : struct
        {
            if (!needle.HasValue)
            {
                throw NoneLeftOperandOfIn();
            }

            return contains(container, needle.GetValueOrDefault());
        }

        /// <summary>
        /// Membership test <c>needle in container</c> for an optional reference-type needle and
        /// a container that refuses <c>None</c> (a subsequence container such as <c>str</c> or
        /// <c>bytes</c>): a <c>None</c> needle raises <see cref="TypeError"/>, as Python does;
        /// otherwise the container answers for the needle.
        /// </summary>
        /// <remarks>
        /// The needle comes before the container so that a call site evaluates its operands in
        /// Python's order. Negation (<c>not in</c>) is the caller's.
        /// A lowering target, not a python builtin: the compiler emits it for <c>in</c> / <c>not in</c>.
        /// </remarks>
        /// <typeparam name="TContainer">The container type</typeparam>
        /// <typeparam name="T">The needle's reference type</typeparam>
        /// <param name="needle">The left operand of <c>in</c>; <c>null</c> is <c>None</c></param>
        /// <param name="container">The right operand of <c>in</c></param>
        /// <param name="contains">The container's membership test, e.g. <c>(c, n) => c.Contains(n)</c></param>
        /// <returns>The result of <paramref name="contains"/></returns>
        /// <exception cref="TypeError">Thrown when <paramref name="needle"/> is <c>None</c></exception>
        /// <example>
        /// <code>
        /// ContainsNoneRaises("b", "abc", static (c, n) => c.Contains(n))            // true
        /// ContainsNoneRaises((string?)null, "abc", static (c, n) => c.Contains(n))  // TypeError
        /// </code>
        /// </example>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool ContainsNoneRaises<TContainer, T>(T? needle, TContainer container, Func<TContainer, T, bool> contains)
            where T : class
        {
            if (needle is null)
            {
                throw NoneLeftOperandOfIn();
            }

            return contains(container, needle);
        }

        // One message for every container: CPython's wording differs per container type
        // ("'in <string>' requires string as left operand, not NoneType" for str, "a bytes-like
        // object is required, not 'NoneType'" for bytes), and only the exception type is
        // compared (docs/design/python-fidelity-scope.md).
        private static TypeError NoneLeftOperandOfIn()
        {
            return new TypeError("'in' requires a non-None left operand for this container, not 'NoneType'");
        }
    }
}
