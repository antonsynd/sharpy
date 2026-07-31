using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Static helper for safe float/double parsing. Called by float.parse() in Sharpy.
    /// Returns Result[float, ValueError] instead of throwing.
    /// </summary>
    public static class DoubleParse
    {
        /// <summary>
        /// Parse a string to double, returning Ok(value) on success or Err(ValueError) on failure.
        /// </summary>
        public static Result<double, ValueError> Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return Result<double, ValueError>.Err(
                    new ValueError($"could not convert string to float: '{s}'"));
            }

            s = s.Trim();

            if (double.TryParse(s, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double result))
            {
                return Result<double, ValueError>.Ok(result);
            }

            return Result<double, ValueError>.Err(
                new ValueError($"could not convert string to float: '{s}'"));
        }

        /// <summary>
        /// Recognize CPython's special float spellings: an optional <c>+</c>/<c>-</c> sign followed
        /// by <c>inf</c>, <c>infinity</c>, or <c>nan</c>, compared case-insensitively.
        /// </summary>
        /// <remarks>
        /// <para>This is the single acceptance table for special float values; both string-to-double
        /// seams (<c>Builtins.Double(string)</c> and <see cref="Parse"/>) consult it before handing
        /// off to <c>double.TryParse</c>, so the two cannot drift in which spellings they accept.</para>
        /// <para>Only the abbreviated <c>inf</c>/<c>-inf</c>/<c>+inf</c> forms are new capability:
        /// <c>double.TryParse</c> with <see cref="CultureInfo.InvariantCulture"/> already matches
        /// that culture's <c>Infinity</c>/<c>NaN</c> symbols case-insensitively and honours a leading
        /// sign, so the full <c>infinity</c> and every <c>nan</c> spelling parsed before this
        /// predicate existed. They are routed through here anyway so the accepted set is one
        /// testable list rather than an inherited BCL detail (#1203).</para>
        /// <para><paramref name="trimmed"/> is expected to be already trimmed — both callers
        /// <c>Trim()</c> first, which is what makes <c>"  inf  "</c> work. The predicate itself does
        /// no whitespace handling and deliberately accepts nothing CPython rejects: <c>"in f"</c>
        /// and <c>"infinit"</c> stay <c>ValueError</c>s.</para>
        /// </remarks>
        /// <param name="trimmed">The already-trimmed candidate string.</param>
        /// <param name="value">The parsed special value when recognized; otherwise <c>0.0</c>.</param>
        /// <returns><c>true</c> when the string is a recognized special float spelling.</returns>
        internal static bool TryParseSpecialFloat(string trimmed, out double value)
        {
            value = 0.0;

            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            var negative = false;
            var start = 0;
            var first = trimmed[0];
            if (first == '+' || first == '-')
            {
                negative = first == '-';
                start = 1;
            }

            if (MatchesRest(trimmed, start, "inf") || MatchesRest(trimmed, start, "infinity"))
            {
                value = negative ? double.NegativeInfinity : double.PositiveInfinity;
                return true;
            }

            if (MatchesRest(trimmed, start, "nan"))
            {
                // CPython yields `nan` for `-nan` too -- the sign is discarded, not applied.
                value = double.NaN;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Case-insensitive comparison of <paramref name="s"/> from <paramref name="start"/> to the
        /// end against <paramref name="literal"/>, without allocating a substring (this sits on the
        /// string-conversion path).
        /// </summary>
        private static bool MatchesRest(string s, int start, string literal)
        {
            return s.Length - start == literal.Length
                && string.Compare(s, start, literal, 0, literal.Length,
                    StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
