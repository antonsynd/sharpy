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
        /// <para>
        /// Only for scalars written PLAIN. A quoted scalar is a string by YAML's own rules, and
        /// resolving one would make <c>yaml.safe_load("\"0.1\"")</c> a number — callers must check
        /// the scalar's style before asking.
        /// </para>
        ///
        /// <para>
        /// <b>Two YAML 1.1 families are deliberately NOT resolved here.</b> Recorded as decisions
        /// rather than left as gaps, because an unlabelled omission in a resolver is
        /// indistinguishable from a bug nobody has noticed, and the next reader would either
        /// "fix" it or leave it alone for the wrong reason. Both are pinned by
        /// <c>YamlScalarResolverFamilyTests.SexagesimalAndTimestamp_AreDeliberatelyNotResolved</c>.
        /// </para>
        ///
        /// <list type="bullet">
        /// <item><description>
        /// <b>Sexagesimal — DECLINED</b> (owner ruling 2026-08-13, #1465). PyYAML reads
        /// <c>12:30</c> as 750 and <c>1:2:3</c> as 3723, following YAML 1.1's base-60 integer and
        /// float arms. YAML 1.2 dropped the type outright, and it is the family most likely to
        /// surprise: a time-of-day, a port range, or a git ref written plain silently becomes an
        /// integer. Sharpy keeps them strings. This is a permanent stated position, so it lives
        /// in a test that asserts it and NOT in the differential-execution allowlist — an
        /// allowlist entry means "ought to agree, does not yet, will drain", which is the
        /// opposite claim.
        /// </description></item>
        /// <item><description>
        /// <b>Timestamp — DEFERRED</b> (#1465). PyYAML reads <c>2024-01-01</c> as a
        /// <c>datetime.date</c>. The blocker is not the regex but the target type: this method
        /// returns <c>null</c>/<c>bool</c>/<c>int</c>/<c>long</c>/<c>double</c>/<c>string</c>, and
        /// what a date should become in that position is undecided — Sharpy's <c>datetime</c>
        /// module has no settled analogue for an untyped-load slot, and picking one here would
        /// decide it by accident for every caller. Deferred until that type decision is made,
        /// not dropped.
        /// </description></item>
        /// </list>
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

            // The int families (#1465): binary, leading-zero octal, hex, plain decimal, and the
            // underscore digit separator that runs through all four.
            //
            // This REPLACED a bare `long.TryParse(value, NumberStyles.AllowLeadingSign)`, and the
            // replacement is a behaviour change rather than an addition — the part of #1465 that
            // neither the issue nor the plan describes. TryParse reads a leading zero as decimal,
            // so `010` answered TEN where YAML 1.1 says EIGHT, and `08` answered eight where YAML
            // 1.1 says it is not an integer at all. Silent wrong values, not a missing feature.
            if (Yaml11Int.IsMatch(value) && TryParseYaml11Int(value, out object? intValue))
            {
                return intValue;
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
            // hexadecimal-looking strings are not silently coerced. Separators are stripped
            // before parsing for the same reason the int families strip them — the mantissa
            // takes them too (#1465): PyYAML reads `1_0.5` as 10.5.
            if (LooksLikeFloat(value) &&
                double.TryParse(
                    StripSeparators(value), NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double doubleValue))
            {
                return doubleValue;
            }

            return value;
        }

        /// <summary>
        /// YAML 1.1's int production (#1465), minus the sexagesimal arm
        /// <c>[-+]?[1-9][0-9_]*(?::[0-5]?[0-9])+</c>, which is DECLINED — see the decision record
        /// on <see cref="Resolve"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Transcribed from PyYAML 6.0.3's own implicit resolver
        /// (<c>yaml.resolver.Resolver.yaml_implicit_resolvers</c>) rather than from the issue's
        /// prose, which is wrong in three places: it spells octal <c>0o…</c> when YAML 1.1 has
        /// only the leading-zero form, it omits binary, and it claims <c>y</c>/<c>n</c> are
        /// bools. Each would have shipped as a defect.
        /// </para>
        /// <para>
        /// The arm order matters and mirrors PyYAML's constructor dispatch: <c>0b</c> and
        /// <c>0x</c> are recognised before the bare leading zero, or <c>0b101</c> would be read
        /// as an octal containing <c>b</c>.
        /// </para>
        /// </remarks>
        private static readonly Regex Yaml11Int = new Regex(
            @"^(?:[-+]?0b[0-1_]+|[-+]?0x[0-9a-fA-F_]+|[-+]?0[0-7_]+|[-+]?(?:0|[1-9][0-9_]*))$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Removes YAML 1.1's underscore digit separators. PyYAML strips them wholesale before
        /// parsing rather than validating their placement, so <c>1__0</c> is 10 and <c>1_</c> is
        /// 1 — permissive, and matched here deliberately (measured, PyYAML 6.0.3).
        /// </summary>
        private static string StripSeparators(string value) =>
            value.IndexOf('_') < 0 ? value : value.Replace("_", string.Empty);

        /// <summary>
        /// Parses a scalar already matched by <see cref="Yaml11Int"/>, narrowing to
        /// <see cref="int"/> when it fits and falling back to <see cref="long"/>.
        /// </summary>
        /// <returns>
        /// <c>false</c> when the scalar cannot be represented, which makes the caller fall
        /// through and read it as a string. Two cases reach that:
        /// <list type="bullet">
        /// <item><description><b>Overflow.</b> Python's ints are arbitrary precision, so PyYAML
        /// answers <c>0xFFFFFFFFFFFFFFFF</c> with 18446744073709551615 and Sharpy cannot. Falling
        /// through to the string is the SAME rule the decimal path already followed — a 20-digit
        /// decimal has always come back a string here — so overflow behaviour stays uniform
        /// across the families rather than gaining a per-radix exception.</description></item>
        /// <item><description><b>Nothing left after the separators.</b> <c>0x_</c> and <c>0b_</c>
        /// match the regex and strip to an empty digit run. PyYAML 6.0.3 RAISES
        /// <c>ValueError: invalid literal for int() with base 16: ''</c> on both (measured) — its
        /// regex admits what its constructor rejects. Sharpy reads them as the strings they look
        /// like instead: propagating another library's internal inconsistency as an exception out
        /// of <c>safe_load</c> would be a worse answer than the obvious one.</description></item>
        /// </list>
        /// </returns>
        private static bool TryParseYaml11Int(string value, out object? result)
        {
            result = null;

            string digits = StripSeparators(value);
            bool negative = digits[0] == '-';
            if (digits[0] == '-' || digits[0] == '+')
            {
                digits = digits.Substring(1);
            }

            int radix = 10;
            if (digits.Length >= 2 && digits[0] == '0' && (digits[1] == 'b' || digits[1] == 'x'))
            {
                radix = digits[1] == 'b' ? 2 : 16;
                digits = digits.Substring(2);
            }
            else if (digits.Length > 1 && digits[0] == '0')
            {
                radix = 8;
            }

            if (digits.Length == 0)
            {
                return false;
            }

            // Accumulated unsigned so that long.MinValue survives: its magnitude is one past
            // long.MaxValue, and a signed accumulator would overflow on the last digit of
            // `-9223372036854775808` — a value the decimal path being replaced here read fine.
            ulong magnitude = 0;
            foreach (char c in digits)
            {
                int digit;
                if (c >= '0' && c <= '9')
                {
                    digit = c - '0';
                }
                else if (c >= 'a' && c <= 'f')
                {
                    digit = c - 'a' + 10;
                }
                else if (c >= 'A' && c <= 'F')
                {
                    digit = c - 'A' + 10;
                }
                else
                {
                    return false;
                }

                if (digit >= radix)
                {
                    return false;
                }

                try
                {
                    magnitude = checked((magnitude * (ulong)radix) + (ulong)digit);
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            ulong limit = negative ? (ulong)long.MaxValue + 1 : (ulong)long.MaxValue;
            if (magnitude > limit)
            {
                return false;
            }

            long signed = negative ? unchecked(-(long)magnitude) : (long)magnitude;
            result = signed >= int.MinValue && signed <= int.MaxValue ? (int)signed : (object)signed;
            return true;
        }

        /// <summary>
        /// YAML 1.1's float production, minus the sexagesimal arm
        /// <c>[-+]?[0-9][0-9_]*(?::[0-5]?[0-9])+\.[0-9_]*</c>, which is DECLINED along with its
        /// integer counterpart — see the decision record on <see cref="Resolve"/>. Written as the
        /// spec's own regex rather than as a character scan, because every previous attempt to
        /// paraphrase it got a boundary wrong.
        ///
        /// <para>Spec (yaml.org/type/float.html), the two arms that apply here:</para>
        /// <code>
        ///   [-+]?([0-9][0-9_]*)\.[0-9_]*([eE][-+][0-9]+)?     # digits, then a REQUIRED dot
        ///   |\.[0-9][0-9_]*([eE][-+][0-9]+)?                   # leading dot, NO sign permitted
        /// </code>
        ///
        /// <para>The underscore separators were deferred to #1465 and have now landed with the
        /// int families: the mantissa takes them on both arms, so <c>1_0.5</c> is 10.5 and
        /// <c>.5_5</c> is 0.55 (measured, PyYAML 6.0.3). The leading-dot arm still requires a
        /// DIGIT first — <c>._5</c> is a string.</para>
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
            @"^(?:[-+]?[0-9][0-9_]*\.[0-9_]*(?:[eE][-+][0-9]+)?|\.[0-9][0-9_]*(?:[eE][-+][0-9]+)?)$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Whether <paramref name="value"/> is spelled as a YAML 1.1 float. Keeps
        /// <c>double.TryParse</c> — which is far more permissive than YAML — from claiming strings
        /// YAML does not call numbers.
        /// </summary>
        private static bool LooksLikeFloat(string value) => Yaml11Float.IsMatch(value);
    }
}
