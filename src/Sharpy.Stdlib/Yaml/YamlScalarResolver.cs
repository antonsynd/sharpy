using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// The single authority on what an untagged plain YAML scalar denotes (#1339).
    ///
    /// <para>
    /// <c>safe_load</c> used to delegate this to YamlDotNet's
    /// <c>WithAttemptingUnquotedStringTypeDeserialization()</c>, which tries <see cref="float"/>
    /// before <see cref="double"/>. Every plain float therefore took a single-precision detour:
    /// <c>yaml.safe_load("0.1")</c> returned <c>0.10000000149011612</c>, and widening it back to
    /// double in <c>NormalizeScalar</c> preserved the error rather than removing it. PyYAML
    /// resolves at double precision, so the round trip <c>safe_load(safe_dump(x)) == x</c> — which
    /// PyYAML holds — did not hold here.
    /// </para>
    ///
    /// <para>
    /// The resolution rules already existed, correctly, in <see cref="YamlRoundtrip"/>'s scalar
    /// path. Rather than write a third copy, both callers now share this one — the parallel-site
    /// discipline from #1145: a rule spelled twice is a rule that will disagree with itself.
    /// </para>
    /// </summary>
    internal static class YamlScalarResolver
    {
        /// <summary>
        /// Resolves an untagged plain scalar to <c>null</c>, <see cref="bool"/>, <see cref="int"/>,
        /// <see cref="long"/>, <see cref="double"/>, or the original <see cref="string"/>.
        /// </summary>
        /// <remarks>
        /// Only for scalars written PLAIN. A quoted scalar is a string by YAML's own rules, and
        /// resolving one would make <c>yaml.safe_load("\"0.1\"")</c> a number — callers must check
        /// the scalar's style before asking.
        /// </remarks>
        internal static object? Resolve(string value)
        {
            if (value.Length == 0 || value == "~" ||
                value == "null" || value == "Null" || value == "NULL")
            {
                return null;
            }

            if (value == "true" || value == "True" || value == "TRUE")
            {
                return true;
            }
            if (value == "false" || value == "False" || value == "FALSE")
            {
                return false;
            }

            if (long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long longValue))
            {
                if (longValue >= int.MinValue && longValue <= int.MaxValue)
                {
                    return (int)longValue;
                }
                return longValue;
            }

            if (value == ".inf" || value == ".Inf" || value == ".INF" || value == "+.inf")
            {
                return double.PositiveInfinity;
            }
            if (value == "-.inf" || value == "-.Inf" || value == "-.INF")
            {
                return double.NegativeInfinity;
            }
            if (value == ".nan" || value == ".NaN" || value == ".NAN")
            {
                return double.NaN;
            }

            // Only treat as float when it actually looks like one, so that values such as
            // hexadecimal-looking strings are not silently coerced.
            if (LooksLikeFloat(value) &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
            {
                return doubleValue;
            }

            return value;
        }

        /// <summary>
        /// Whether <paramref name="value"/> has the shape of a YAML float: at least one digit, and
        /// only characters a float spelling may contain. Keeps <c>double.TryParse</c> from claiming
        /// strings it would accept but YAML would not call numbers.
        /// </summary>
        private static bool LooksLikeFloat(string value)
        {
            bool hasDigit = false;
            foreach (char c in value)
            {
                if (c >= '0' && c <= '9')
                {
                    hasDigit = true;
                }
                else if (c != '.' && c != 'e' && c != 'E' && c != '+' && c != '-')
                {
                    return false;
                }
            }
            return hasDigit && (value.IndexOf('.') >= 0 || value.IndexOf('e') >= 0 || value.IndexOf('E') >= 0);
        }
    }
}
