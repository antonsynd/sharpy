using System;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Convert an arbitrary object to its string representation.
        /// Returns <c>"None"</c> for null, Python-style <c>"True"</c>/<c>"False"</c>
        /// for booleans, and <see cref="object.ToString"/> for everything else.
        /// </summary>
        /// <param name="x">The object to convert</param>
        /// <returns>The string representation</returns>
        /// <example>
        /// <code>
        /// str(42)        # "42"
        /// str(3.14)      # "3.14"
        /// str(True)      # "True"
        /// str(None)      # "None"
        /// </code>
        /// </example>
        public static string Str(object x)
        {
            if (x is null)
            {
                return "None";
            }

            if (Optional.TryFormat(x, out var optStr))
            {
                return optStr;
            }

            if (x is bool b)
            {
                return b ? "True" : "False";
            }

            if (x is double d)
            {
                return FormatFloat(d);
            }

            if (x is float f)
            {
                return FormatFloat(f);
            }

            // ValueTuples (System.ValueTuple<...>): format with Python-style
            // parentheses. Like list/set/dict, str() of a tuple formats its
            // elements with repr() semantics (e.g. str(('x', 'y')) == "('x', 'y')"),
            // so this delegates to the shared repr formatter (which also handles
            // the single-element trailing comma: str((1,)) == "(1,)").
            var type = x.GetType();
            if (type.IsValueType && type.FullName != null
                && type.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
            {
                return FormatValueTuple(x, type);
            }

            return x.ToString() ?? "";
        }

        /// <summary>
        /// Return the string unchanged.
        /// </summary>
        public static string Str(string s)
        {
            return s;
        }

        /// <summary>
        /// Convert a <see cref="char"/> to string without boxing.
        /// </summary>
        public static string Str(char c)
        {
            return c.ToString();
        }

        /// <summary>
        /// Convert an <see cref="int"/> to string without boxing.
        /// </summary>
        public static string Str(int i)
        {
            return i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert a <see cref="long"/> to string without boxing.
        /// </summary>
        public static string Str(long l)
        {
            return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert a <see cref="double"/> to string without boxing.
        /// Formats with Python-compatible trailing <c>.0</c> for whole numbers.
        /// </summary>
        public static string Str(double d)
        {
            return FormatFloat(d);
        }

        /// <summary>
        /// Convert a <see cref="float"/> to string without boxing.
        /// Formats with Python-compatible trailing <c>.0</c> for whole numbers.
        /// </summary>
        public static string Str(float f)
        {
            return FormatFloat(f);
        }

        /// <summary>
        /// Largest <c>decpt</c> that still renders positionally for a <see cref="double"/>.
        /// CPython's <c>format_float_short</c> uses <c>decpt &gt; 16 =&gt; exponential</c>.
        /// </summary>
        private const int DoubleMaxPositionalDecpt = 16;

        /// <summary>
        /// Largest <c>decpt</c> that still renders positionally for a <see cref="float"/>.
        /// See <see cref="FormatFloat(float)"/> for why this is 9 and not 16.
        /// </summary>
        private const int SingleMaxPositionalDecpt = 9;

        /// <summary>
        /// Format a floating-point value with Python-compatible representation.
        /// NaN, Infinity, and -Infinity use Python's lowercase forms.
        /// Whole-number values get a trailing <c>.0</c>.
        /// </summary>
        /// <remarks>
        /// This is the single authority for Python-style float formatting; every other
        /// float-rendering site delegates here (guarded by
        /// <c>FloatFormattingAuthorityTests</c>).
        /// <para>
        /// The digits come from .NET's shortest-round-trip formatter (<c>"R"</c>), which is
        /// correct, but the positional-vs-exponential <em>layout</em> is Sharpy's own decision,
        /// ported from CPython's <c>format_float_short</c>: writing a value as
        /// <c>0.d1…dn × 10^decpt</c>, render positionally when <c>-4 &lt; decpt &lt;= 16</c>
        /// and exponentially otherwise. .NET stays positional one decade longer
        /// (<c>decpt &lt;= 17</c>), which is the <c>[1e16, 1e17)</c> divergence band of #1204.
        /// </para>
        /// <para>
        /// Re-rendering from the digits, rather than patching the one divergent band, keeps
        /// the layout policy here instead of inheriting whatever a future runtime picks.
        /// </para>
        /// </remarks>
        public static string FormatFloat(double value)
        {
            if (double.IsNaN(value))
            {
                return "nan";
            }

            if (double.IsPositiveInfinity(value))
            {
                return "inf";
            }

            if (double.IsNegativeInfinity(value))
            {
                return "-inf";
            }

            return RenderShortestRoundTrip(
                value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                DoubleMaxPositionalDecpt);
        }

        /// <summary>
        /// Format a <see cref="float"/> value with Python-compatible representation.
        /// Overload to avoid float→double widening precision issues.
        /// </summary>
        /// <remarks>
        /// Shares <see cref="RenderShortestRoundTrip"/> with <see cref="FormatFloat(double)"/>
        /// — the two overloads share a renderer, not a threshold.
        /// <para>
        /// The single switches to exponential at <c>decpt &gt; 9</c>, not the double's 16.
        /// That is a deliberate Sharpy decision rather than CPython parity, because CPython
        /// has no <c>float32</c> and therefore has no answer to copy. The derivation mirrors
        /// CPython's: its 16 tracks the ≤17 significant digits a double's shortest
        /// round-trip form can need, so positional layout never pads with digits the type
        /// does not carry; a single needs ≤9, so 9 is its analogue. Using the double's 16
        /// here would spread float32 output positionally across <c>[1e9, 1e16)</c> — e.g.
        /// <c>1.5e15f</c> would print as <c>1500000000000000.0</c>, sixteen digits for a
        /// type carrying about seven (Axiom 3).
        /// </para>
        /// <para>
        /// 9 is also .NET's own single threshold, so float32 output is byte-identical to
        /// what it was before this renderer existed.
        /// </para>
        /// </remarks>
        public static string FormatFloat(float value)
        {
            if (float.IsNaN(value))
            {
                return "nan";
            }

            if (float.IsPositiveInfinity(value))
            {
                return "inf";
            }

            if (float.IsNegativeInfinity(value))
            {
                return "-inf";
            }

            return RenderShortestRoundTrip(
                value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                SingleMaxPositionalDecpt);
        }

        /// <summary>
        /// Re-render a .NET shortest-round-trip string (<c>"R"</c>) using CPython's layout rule.
        /// </summary>
        /// <param name="roundTrip">
        /// The output of <c>ToString("R", InvariantCulture)</c>. Must not be NaN or an infinity —
        /// callers handle those first, since Python spells them <c>nan</c>/<c>inf</c>/<c>-inf</c>.
        /// </param>
        /// <param name="maxPositionalDecpt">
        /// The largest <c>decpt</c> that still renders positionally. This is the only thing that
        /// differs between the <see cref="double"/> and <see cref="float"/> overloads.
        /// </param>
        /// <remarks>
        /// The input is decomposed into a sign, a significant-digit run, and <c>decpt</c>, where
        /// the value is <c>0.d1…dn × 10^decpt</c> (David Gay's <c>dtoa</c> convention, which is
        /// what CPython's threshold constants are expressed in: <c>1.0</c> has <c>decpt == 1</c>,
        /// <c>1e16</c> has <c>decpt == 17</c>, <c>1e-5</c> has <c>decpt == -4</c>).
        /// Exponential output follows CPython: lowercase <c>e</c>, an explicit sign, and at
        /// least two exponent digits (<c>1e+16</c>, <c>1e-05</c>).
        /// </remarks>
        private static string RenderShortestRoundTrip(string roundTrip, int maxPositionalDecpt)
        {
            var digits = new char[roundTrip.Length];
            var count = 0;
            var decpt = 0;
            var negative = false;
            var seenPoint = false;
            var index = 0;

            if (index < roundTrip.Length && (roundTrip[index] == '-' || roundTrip[index] == '+'))
            {
                negative = roundTrip[index] == '-';
                index++;
            }

            for (; index < roundTrip.Length; index++)
            {
                var c = roundTrip[index];

                if (c == '.')
                {
                    seenPoint = true;
                    continue;
                }

                if (c == 'e' || c == 'E')
                {
                    decpt += int.Parse(
                        roundTrip.Substring(index + 1),
                        System.Globalization.NumberStyles.AllowLeadingSign,
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                }

                digits[count++] = c;

                if (!seenPoint)
                {
                    decpt++;
                }
            }

            // Normalize to a bare significand: leading zeros shift the decimal point,
            // trailing zeros carry no information (they are re-materialized on render).
            var start = 0;
            while (start < count && digits[start] == '0')
            {
                start++;
                decpt--;
            }

            var end = count;
            while (end > start && digits[end - 1] == '0')
            {
                end--;
            }

            // Zero has no significant digits, so decpt is meaningless for it. The sign still
            // matters: Python prints -0.0, and .NET's "R" reports the sign bit as a leading '-'.
            if (start == end)
            {
                return negative ? "-0.0" : "0.0";
            }

            var length = end - start;
            var sb = new System.Text.StringBuilder(length + 8);

            if (negative)
            {
                sb.Append('-');
            }

            if (decpt > -4 && decpt <= maxPositionalDecpt)
            {
                if (decpt <= 0)
                {
                    sb.Append("0.");
                    sb.Append('0', -decpt);
                    sb.Append(digits, start, length);
                }
                else if (decpt >= length)
                {
                    sb.Append(digits, start, length);
                    sb.Append('0', decpt - length);
                    sb.Append(".0");
                }
                else
                {
                    sb.Append(digits, start, decpt);
                    sb.Append('.');
                    sb.Append(digits, start + decpt, length - decpt);
                }
            }
            else
            {
                sb.Append(digits[start]);

                if (length > 1)
                {
                    sb.Append('.');
                    sb.Append(digits, start + 1, length - 1);
                }

                // CPython's exponent: one digit of significand before the point, an explicit
                // sign, and a minimum width of two digits (1e+16, 1e-05, 5e-324).
                var exponent = decpt - 1;
                sb.Append('e');
                sb.Append(exponent < 0 ? '-' : '+');

                var magnitude = exponent < 0 ? -exponent : exponent;

                if (magnitude < 10)
                {
                    sb.Append('0');
                }

                sb.Append(magnitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convert a <see cref="bool"/> to string.
        /// Returns Python-style <c>"True"</c> or <c>"False"</c>.
        /// </summary>
        public static string Str(bool b)
        {
            return b ? "True" : "False";
        }
    }
}
