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
        /// Used for <c>for c in s:</c> codegen.
        /// </summary>
        /// <remarks>
        /// Iterates by UTF-16 code unit, not Unicode code point. Surrogate pairs
        /// (e.g., emoji) yield two separate single-char strings. This follows
        /// Axiom 1 (.NET UTF-16 semantics take precedence over Python code-point iteration).
        /// </remarks>
        public static IEnumerable<string> Iterate(string s)
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
        /// </remarks>
        public static IEnumerable<string> Reversed(string s)
        {
            for (int i = s.Length - 1; i >= 0; i--)
            {
                yield return s[i].ToString();
            }
        }
    }
}
