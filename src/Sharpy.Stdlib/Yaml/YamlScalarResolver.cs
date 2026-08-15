using System;
using System.Globalization;
using System.Text.RegularExpressions;

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

            // The bool family, spelled out one casing at a time rather than compared
            // case-insensitively. PyYAML accepts exactly three casings per word — all-lower,
            // Title, all-UPPER — so `yEs` and `tRue` are STRINGS, which a case-insensitive
            // compare would wrongly claim (#1465, cells measured against PyYAML 6.0.3).
            if (value == "true" || value == "True" || value == "TRUE" ||
                value == "yes" || value == "Yes" || value == "YES" ||
                value == "on" || value == "On" || value == "ON")
            {
                return true;
            }
            if (value == "false" || value == "False" || value == "FALSE" ||
                value == "no" || value == "No" || value == "NO" ||
                value == "off" || value == "Off" || value == "OFF")
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
        /// YAML 1.1's float production, minus the families deferred to #1465 (underscore digit
        /// separators and the sexagesimal form). Written as the spec's own regex rather than as a
        /// character scan, because every previous attempt to paraphrase it got a boundary wrong.
        ///
        /// <para>Spec (yaml.org/type/float.html), the two arms that apply here:</para>
        /// <code>
        ///   [-+]?([0-9][0-9_]*)\.[0-9_]*([eE][-+][0-9]+)?     # digits, then a REQUIRED dot
        ///   |\.[0-9_]+([eE][-+][0-9]+)?                        # leading dot, NO sign permitted
        /// </code>
        ///
        /// <para>Three boundaries a paraphrase reliably loses, all measured against PyYAML 6.0.3
        /// (#1423):</para>
        /// <list type="bullet">
        /// <item><description>The mantissa's <b>dot is mandatory</b>: <c>1e-7</c> is a STRING, which
        /// is the defect this rule exists to fix — <c>double.TryParse</c> happily claimed it.</description></item>
        /// <item><description>The exponent's <b>sign is mandatory</b>: <c>1.0e7</c> and <c>1.5E3</c>
        /// are strings; only <c>1.0e+7</c> / <c>1.0e-7</c> are floats.</description></item>
        /// <item><description>The leading-dot arm <b>admits no sign</b>: <c>.5</c> is a float but
        /// <c>+.5</c> and <c>-.5</c> are strings.</description></item>
        /// </list>
        ///
        /// <para><c>.inf</c>/<c>.nan</c> are handled by their own arms in <see cref="Resolve"/>
        /// before this is reached.</para>
        /// </summary>
        private static readonly Regex Yaml11Float = new Regex(
            @"^(?:[-+]?[0-9]+\.[0-9]*(?:[eE][-+][0-9]+)?|\.[0-9]+(?:[eE][-+][0-9]+)?)$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Whether <paramref name="value"/> is spelled as a YAML 1.1 float. Keeps
        /// <c>double.TryParse</c> — which is far more permissive than YAML — from claiming strings
        /// YAML does not call numbers.
        /// </summary>
        private static bool LooksLikeFloat(string value) => Yaml11Float.IsMatch(value);
    }
}
