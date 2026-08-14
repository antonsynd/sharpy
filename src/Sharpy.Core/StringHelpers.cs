using System;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Static helpers for string operations emitted by the Sharpy compiler.
    /// These handle operations that System.String doesn't natively support
    /// (repetition, negative indexing, code-point iteration).
    /// </summary>
    public static class StringHelpers
    {
        /// <summary>
        /// Repeats a string a specified number of times.
        /// Python: <c>"ab" * 3  # "ababab"</c>
        /// </summary>
        public static string Repeat(string s, int count)
        {
            if (count <= 0 || s.Length == 0)
            {
                return "";
            }

            if (count == 1)
            {
                return s;
            }

            var sb = new StringBuilder(s.Length * count);
            for (int i = 0; i < count; i++)
            {
                sb.Append(s);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Repeats a string a specified number of times (long count).
        /// Throws <see cref="OverflowError"/> if count exceeds int range.
        /// </summary>
        public static string Repeat(string s, long count)
        {
            if (count > int.MaxValue || count < int.MinValue)
                throw new OverflowError("repeated string is too long");
            return Repeat(s, (int)count);
        }

        /// <summary>
        /// Gets the character at the specified index as a single-character string.
        /// Supports negative indexing (Python semantics).
        /// </summary>
        /// <exception cref="IndexError">Thrown if the index is out of range.</exception>
        public static string GetItem(string s, int index)
        {
            int actual = index < 0 ? s.Length + index : index;
            if (actual < 0 || actual >= s.Length)
            {
                throw new IndexError("string index out of range");
            }
            return s[actual].ToString();
        }

        /// <summary>
        /// Yields single-character strings for each char in the string.
        /// Python iterates strings yielding single-char strings, not chars.
        /// Used for <c>for c in s:</c> and <c>iter(s)</c> codegen.
        /// </summary>
        /// <remarks>
        /// Iterates by UTF-16 code unit, not Unicode code point. Surrogate pairs
        /// (e.g., emoji) yield two separate single-char strings. This follows
        /// Axiom 1 (.NET UTF-16 semantics take precedence over Python code-point iteration).
        /// <para>
        /// Returns <c>Iterator&lt;string&gt;</c> for the same reason <see cref="Reversed"/> does:
        /// the compiler TYPES <c>iter(s)</c> as <c>Iterator[str]</c> while emitting a call to this
        /// method, and a return type that disagreed would produce CS0266 behind SPY0908 for
        /// ordinary source such as <c>it: Iterator[str] = iter("abc")</c> (#1468). <c>Iterator&lt;T&gt;</c>
        /// is an <c>IEnumerable&lt;T&gt;</c>, so the <c>for c in s:</c> lowering — which calls this
        /// method fresh on each execution of the loop — is unaffected.
        /// </para>
        /// </remarks>
        public static Iterator<string> Iterate(string s)
            => new EnumeratorIterator<string>(IterateCore(s).GetEnumerator());

        private static IEnumerable<string> IterateCore(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                yield return s[i].ToString();
            }
        }

        /// <summary>
        /// Yields single-character strings in reverse order.
        /// Used for <c>reversed(s)</c> codegen.
        /// </summary>
        /// <remarks>
        /// Iterates by UTF-16 code unit, not Unicode code point. See
        /// <see cref="Iterate"/> remarks for Axiom 1 rationale.
        /// <para>
        /// Returns <c>Iterator&lt;string&gt;</c>, not <c>IEnumerable&lt;string&gt;</c>, because the
        /// compiler already TYPES <c>reversed(s)</c> as <c>Iterator[str]</c>
        /// (<c>BuiltinReturnTypeInference.InferReversed</c>) while emitting a call to this method.
        /// While the return type disagreed, `a: Iterator[str] = reversed("abc")` produced CS0266
        /// behind SPY0908 — an internal-error report for ordinary source (#1354). The non-string
        /// overloads never had the problem because <c>Builtins.Reversed&lt;T&gt;</c> already returned
        /// <c>Iterator&lt;T&gt;</c>; this was the one unflipped surface in the set.
        /// </para>
        /// </remarks>
        public static Iterator<string> Reversed(string s)
            => new EnumeratorIterator<string>(ReversedCore(s).GetEnumerator(), "<reversed object>");

        private static IEnumerable<string> ReversedCore(string s)
        {
            for (int i = s.Length - 1; i >= 0; i--)
            {
                yield return s[i].ToString();
            }
        }
    }
}
