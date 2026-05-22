using System.Collections.Generic;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Grapheme cluster (user-perceived character) operations.
    /// Wraps <see cref="System.Globalization.StringInfo"/> for working with
    /// text at the level of what users perceive as a single character — including
    /// combining marks, emoji sequences, and ZWJ (zero-width joiner) sequences.
    /// </summary>
    /// <remarks>
    /// .NET's <see cref="StringInfo"/> treats text elements as Unicode grapheme
    /// clusters. Note that the underlying implementation may not fully support
    /// the latest Unicode segmentation rules (e.g., extended grapheme clusters
    /// from UAX #29), but it handles the most common cases including surrogate
    /// pairs and combining sequences.
    /// </remarks>
    public static partial class Grapheme
    {
        /// <summary>
        /// Splits a string into a list of grapheme clusters.
        /// </summary>
        /// <param name="text">The string to split.</param>
        /// <returns>A list where each element is a single grapheme cluster.</returns>
        /// <example>
        /// <code>
        /// grapheme.graphemes("héllo")  # ["h", "é", "l", "l", "o"]
        /// grapheme.graphemes("👨‍👩‍👧")  # ["👨‍👩‍👧"]
        /// </code>
        /// </example>
        public static List<string> Graphemes(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            var enumerator = StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
            {
                result.Add((string)enumerator.Current);
            }
            return result;
        }

        /// <summary>
        /// Returns the number of grapheme clusters in a string.
        /// </summary>
        /// <param name="text">The string to measure.</param>
        /// <returns>The count of grapheme clusters.</returns>
        /// <example>
        /// <code>
        /// grapheme.length("hello")    # 5
        /// grapheme.length("héllo")    # 5 (é is a single grapheme)
        /// grapheme.length("👨‍👩‍👧")  # 1 (ZWJ sequence)
        /// </code>
        /// </example>
        public static int Length(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return new StringInfo(text).LengthInTextElements;
        }

        /// <summary>
        /// Returns a substring containing all graphemes from <paramref name="start"/> to the end.
        /// </summary>
        /// <param name="text">The string to slice.</param>
        /// <param name="start">The starting grapheme index (inclusive). Negative values count from the end.</param>
        /// <returns>The substring from <paramref name="start"/> to the end of the string.</returns>
        /// <example>
        /// <code>
        /// grapheme.slice("héllo", 2)   # "llo"
        /// </code>
        /// </example>
        public static string Slice(string text, int start)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var info = new StringInfo(text);
            return SliceCore(info, info.LengthInTextElements, start, info.LengthInTextElements);
        }

        /// <summary>
        /// Returns a substring by grapheme cluster index range.
        /// </summary>
        /// <param name="text">The string to slice.</param>
        /// <param name="start">The starting grapheme index (inclusive).</param>
        /// <param name="end">The ending grapheme index (exclusive).</param>
        /// <returns>The substring containing graphemes from <paramref name="start"/> up to (but not including) <paramref name="end"/>.</returns>
        /// <example>
        /// <code>
        /// grapheme.slice("héllo", 0, 3)   # "hél"
        /// </code>
        /// </example>
        /// <remarks>
        /// Indices are clamped to the valid range to match Python's slice semantics.
        /// Negative indices count from the end of the string.
        /// </remarks>
        public static string Slice(string text, int start, int end)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var info = new StringInfo(text);
            return SliceCore(info, info.LengthInTextElements, start, end);
        }

        private static string SliceCore(StringInfo info, int count, int start, int end)
        {
            // Normalize negative indices (Python slice semantics: clamp to [0, count]).
            var s = start < 0 ? System.Math.Max(0, count + start) : System.Math.Min(start, count);
            var e = end < 0 ? System.Math.Max(0, count + end) : System.Math.Min(end, count);

            if (s >= e)
            {
                return string.Empty;
            }

            return info.SubstringByTextElements(s, e - s);
        }

        /// <summary>
        /// Returns a single grapheme cluster at the given index.
        /// </summary>
        /// <param name="text">The string to index.</param>
        /// <param name="index">
        /// The grapheme index. Negative values count from the end
        /// (e.g., <c>-1</c> is the last grapheme).
        /// </param>
        /// <returns>The grapheme cluster at <paramref name="index"/>.</returns>
        /// <exception cref="IndexError">
        /// If <paramref name="index"/> is out of range.
        /// </exception>
        /// <example>
        /// <code>
        /// grapheme.at("héllo", 1)   # "é"
        /// grapheme.at("héllo", -1)  # "o"
        /// </code>
        /// </example>
        public static string At(string text, int index)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new IndexError($"grapheme index {index} out of range");
            }

            var info = new StringInfo(text);
            var count = info.LengthInTextElements;

            if (index >= count || index < -count)
            {
                throw new IndexError($"grapheme index {index} out of range");
            }

            var normalized = index < 0 ? count + index : index;
            return info.SubstringByTextElements(normalized, 1);
        }
    }
}
