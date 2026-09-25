using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// <c>Fraction.__format__</c>: an exact port of CPython 3.14's <c>fractions.py</c> over
    /// <see cref="BigInteger"/> (#2019). <see cref="IFormattable"/> is the CLR spelling of
    /// <c>__format__</c>, so <c>format()</c>, <c>str.format</c>, f-strings and t-strings all reach it.
    /// The grammar is fractions.py's OWN, not the builtin mini-language's: two matchers — a general
    /// one (fill/align, sign, <c>#</c>, width, grouping; no presentation type) and a float-style one
    /// (<c>eEfFgG%</c>, with <c>z</c>, a zero-pad flag only when a digit follows the <c>0</c>, and
    /// 3.14's fractional separators) — both ASCII-digit only, and a spec that is neither is
    /// <c>ValueError: Invalid format specifier '&lt;spec&gt;' for object of type 'Fraction'</c>.
    /// Rounding is exact and half-even on the rational value; no <c>double</c> is involved.
    /// Width and precision are bounded by <c>int</c> (a CLR string length).
    /// </summary>
    public sealed partial class Fraction
    {
        string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => Format(format ?? "");

        private string Format(string spec)
        {
            if (TryMatchGeneral(spec, out var general))
            {
                return FormatGeneral(general);
            }
            // Refuse the temptation to guess if both alignment _and_ zero padding are specified.
            if (TryMatchFloat(spec, out var floatSpec) && (floatSpec.Align == '\0' || !floatSpec.ZeroPad))
            {
                return FormatFloatStyle(floatSpec);
            }
            throw new ValueError("Invalid format specifier " + Builtins.Repr(spec) + " for object of type 'Fraction'");
        }

        private struct FractionSpec
        {
            public char Fill;
            public char Align;
            public char Sign;
            public bool NoNegZero;
            public bool Alt;
            public bool ZeroPad;
            public int MinimumWidth;
            public char ThousandsSep;
            public int Precision;
            public char FracSep;
            public char Type;
        }

        private static bool IsAlign(char c) => c == '<' || c == '>' || c == '=' || c == '^';

        private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

        private static void MatchFillAlign(string s, ref int pos, ref FractionSpec spec)
        {
            if (s.Length - pos >= 2 && IsAlign(s[pos + 1]))
            {
                spec.Fill = s[pos];
                spec.Align = s[pos + 1];
                pos += 2;
            }
            else if (s.Length - pos >= 1 && IsAlign(s[pos]))
            {
                spec.Align = s[pos];
                pos++;
            }
        }

        private static void MatchSign(string s, ref int pos, ref FractionSpec spec)
        {
            if (pos < s.Length && (s[pos] == '-' || s[pos] == '+' || s[pos] == ' '))
            {
                spec.Sign = s[pos];
                pos++;
            }
        }

        private static int ReadAsciiInt(string s, int start, int end)
        {
            if (!int.TryParse(s.Substring(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            {
                throw new ValueError("Too many decimal digits in format string");
            }
            return value;
        }

        /// <summary><c>(?:(?P&lt;fill&gt;.)?(?P&lt;align&gt;[&lt;&gt;=^]))?(?P&lt;sign&gt;[-+ ]?)(?P&lt;alt&gt;\#)?(?P&lt;minimumwidth&gt;0|[1-9][0-9]*)?(?P&lt;thousands_sep&gt;[,_])?</c>, full match.</summary>
        private static bool TryMatchGeneral(string s, out FractionSpec spec)
        {
            spec = default;
            int pos = 0;
            MatchFillAlign(s, ref pos, ref spec);
            MatchSign(s, ref pos, ref spec);
            if (pos < s.Length && s[pos] == '#')
            {
                spec.Alt = true;
                pos++;
            }
            if (pos < s.Length && s[pos] == '0')
            {
                pos++;
            }
            else if (pos < s.Length && s[pos] >= '1' && s[pos] <= '9')
            {
                int start = pos;
                while (pos < s.Length && IsAsciiDigit(s[pos]))
                {
                    pos++;
                }
                spec.MinimumWidth = ReadAsciiInt(s, start, pos);
            }
            if (pos < s.Length && (s[pos] == ',' || s[pos] == '_'))
            {
                spec.ThousandsSep = s[pos];
                pos++;
            }
            return pos == s.Length;
        }

        /// <summary>
        /// <c>(?:(?P&lt;fill&gt;.)?(?P&lt;align&gt;[&lt;&gt;=^]))?(?P&lt;sign&gt;[-+ ]?)(?P&lt;no_neg_zero&gt;z)?(?P&lt;alt&gt;\#)?
        /// (?P&lt;zeropad&gt;0(?=[0-9]))?(?P&lt;minimumwidth&gt;[0-9]+)?(?P&lt;thousands_sep&gt;[,_])?
        /// (?:\.(?=[,_0-9])(?P&lt;precision&gt;[0-9]+)?(?P&lt;frac_separators&gt;[,_])?)?(?P&lt;presentation_type&gt;[eEfFgG%])</c>, full match.
        /// </summary>
        private static bool TryMatchFloat(string s, out FractionSpec spec)
        {
            spec = default;
            spec.Precision = -1;
            int pos = 0;
            MatchFillAlign(s, ref pos, ref spec);
            MatchSign(s, ref pos, ref spec);
            if (pos < s.Length && s[pos] == 'z')
            {
                spec.NoNegZero = true;
                pos++;
            }
            if (pos < s.Length && s[pos] == '#')
            {
                spec.Alt = true;
                pos++;
            }
            if (pos + 1 < s.Length && s[pos] == '0' && IsAsciiDigit(s[pos + 1]))
            {
                spec.ZeroPad = true;
                pos++;
            }
            int widthStart = pos;
            while (pos < s.Length && IsAsciiDigit(s[pos]))
            {
                pos++;
            }
            if (pos > widthStart)
            {
                spec.MinimumWidth = ReadAsciiInt(s, widthStart, pos);
            }
            if (pos < s.Length && (s[pos] == ',' || s[pos] == '_'))
            {
                spec.ThousandsSep = s[pos];
                pos++;
            }
            if (pos + 1 < s.Length && s[pos] == '.' && (s[pos + 1] == ',' || s[pos + 1] == '_' || IsAsciiDigit(s[pos + 1])))
            {
                pos++;
                int precisionStart = pos;
                while (pos < s.Length && IsAsciiDigit(s[pos]))
                {
                    pos++;
                }
                if (pos > precisionStart)
                {
                    spec.Precision = ReadAsciiInt(s, precisionStart, pos);
                }
                if (pos < s.Length && (s[pos] == ',' || s[pos] == '_'))
                {
                    spec.FracSep = s[pos];
                    pos++;
                }
            }
            if (pos < s.Length && "eEfFgG%".IndexOf(s[pos]) >= 0)
            {
                spec.Type = s[pos];
                pos++;
                return pos == s.Length;
            }
            return false;
        }

        private static string Digits(BigInteger n) => n.ToString(CultureInfo.InvariantCulture);

        private static string Group(string digits, char sep)
        {
            if (sep == '\0')
            {
                return digits;
            }
            int firstPos = 1 + (digits.Length - 1) % 3;
            var sb = new StringBuilder(digits.Substring(0, firstPos));
            for (int pos = firstPos; pos < digits.Length; pos += 3)
            {
                sb.Append(sep).Append(digits, pos, 3);
            }
            return sb.ToString();
        }

        private static string Pad(char fill, char align, int minimumWidth, string sign, string body)
        {
            int count = minimumWidth - sign.Length - body.Length;
            string padding = count > 0 ? new string(fill, count) : "";
            switch (align)
            {
                case '<':
                    return sign + body + padding;
                case '^':
                    int half = padding.Length / 2;
                    return padding.Substring(0, half) + sign + body + padding.Substring(half);
                case '=':
                    return sign + padding + body;
                default:
                    return padding + sign + body;
            }
        }

        private string FormatGeneral(FractionSpec spec)
        {
            char fill = spec.Fill == '\0' ? ' ' : spec.Fill;
            string posSign = spec.Sign == '\0' || spec.Sign == '-' ? "" : spec.Sign.ToString();
            BigInteger n = Numerator, d = Denominator;
            string body = Group(Digits(BigInteger.Abs(n)), spec.ThousandsSep);
            if (d > BigInteger.One || spec.Alt)
            {
                body += "/" + Group(Digits(d), spec.ThousandsSep);
            }
            string sign = n.Sign < 0 ? "-" : posSign;
            return Pad(fill, spec.Align, spec.MinimumWidth, sign, body);
        }

        /// <summary>Python's <c>divmod</c> for a positive divisor: the floored quotient and a non-negative remainder.</summary>
        private static BigInteger FloorDivRem(BigInteger n, BigInteger d, out BigInteger remainder)
        {
            BigInteger q = BigInteger.DivRem(n, d, out remainder);
            if (remainder.Sign < 0)
            {
                q -= BigInteger.One;
                remainder += d;
            }
            return q;
        }

        /// <summary>fractions.py <c>_round_to_exponent</c>: n/d rounded half-even to a multiple of 10**exponent.</summary>
        private static bool RoundToExponent(BigInteger n, BigInteger d, int exponent, bool noNegZero, out BigInteger significand)
        {
            if (exponent >= 0)
            {
                d *= BigInteger.Pow(10, exponent);
            }
            else
            {
                n *= BigInteger.Pow(10, -exponent);
            }
            BigInteger q = FloorDivRem(n + (d >> 1), d, out BigInteger r);
            if (r.IsZero && d.IsEven)
            {
                q &= new BigInteger(-2);
            }
            bool sign = noNegZero ? q.Sign < 0 : n.Sign < 0;
            significand = BigInteger.Abs(q);
            return sign;
        }

        /// <summary>fractions.py <c>_round_to_figures</c>: n/d rounded half-even to <paramref name="figures"/> significant figures.</summary>
        private static bool RoundToFigures(BigInteger n, BigInteger d, int figures, out BigInteger significand, out int exponent)
        {
            if (n.IsZero)
            {
                significand = BigInteger.Zero;
                exponent = 1 - figures;
                return false;
            }
            string strN = Digits(BigInteger.Abs(n)), strD = Digits(d);
            int m = strN.Length - strD.Length + (string.CompareOrdinal(strD, strN) <= 0 ? 1 : 0);
            exponent = m - figures;
            bool sign = RoundToExponent(n, d, exponent, false, out significand);
            if (Digits(significand).Length == figures + 1)
            {
                significand /= 10;
                exponent += 1;
            }
            return sign;
        }

        private string FormatFloatStyle(FractionSpec spec)
        {
            char fill = spec.Fill == '\0' ? ' ' : spec.Fill;
            char align = spec.Align == '\0' ? '>' : spec.Align;
            string posSign = spec.Sign == '\0' || spec.Sign == '-' ? "" : spec.Sign.ToString();
            bool zeropad = spec.ZeroPad;
            int precision = spec.Precision < 0 ? 6 : spec.Precision;
            char type = spec.Type;
            bool trimZeros = (type == 'g' || type == 'G') && !spec.Alt;
            bool trimPoint = !spec.Alt;
            string exponentIndicator = type == 'E' || type == 'F' || type == 'G' ? "E" : "e";

            if (align == '=' && fill == '0')
            {
                zeropad = true;
            }

            bool negative;
            BigInteger significand;
            bool scientific;
            int pointPos;
            int exponent;
            if (type == 'f' || type == 'F' || type == '%')
            {
                exponent = -precision;
                if (type == '%')
                {
                    exponent -= 2;
                }
                negative = RoundToExponent(Numerator, Denominator, exponent, spec.NoNegZero, out significand);
                scientific = false;
                pointPos = precision;
            }
            else
            {
                int figures = type == 'g' || type == 'G' ? Math.Max(precision, 1) : precision + 1;
                negative = RoundToFigures(Numerator, Denominator, figures, out significand, out exponent);
                scientific = type == 'e' || type == 'E' || exponent > 0 || exponent + figures <= -4;
                pointPos = scientific ? figures - 1 : -exponent;
            }

            string suffix;
            if (type == '%')
            {
                suffix = "%";
            }
            else if (scientific)
            {
                int e = exponent + pointPos;
                suffix = exponentIndicator + (e < 0 ? "-" : "+") + Math.Abs(e).ToString("D2", CultureInfo.InvariantCulture);
            }
            else
            {
                suffix = "";
            }

            string digits = Digits(significand).PadLeft(pointPos + 1, '0');

            string sign = negative ? "-" : posSign;
            string leading = digits.Substring(0, digits.Length - pointPos);
            string fracPart = digits.Substring(digits.Length - pointPos);
            if (trimZeros)
            {
                fracPart = fracPart.TrimEnd('0');
            }
            string separator = trimPoint && fracPart.Length == 0 ? "" : ".";
            if (spec.FracSep != '\0')
            {
                var grouped = new StringBuilder();
                for (int pos = 0; pos < fracPart.Length; pos += 3)
                {
                    if (pos > 0)
                    {
                        grouped.Append(spec.FracSep);
                    }
                    grouped.Append(fracPart, pos, Math.Min(3, fracPart.Length - pos));
                }
                fracPart = grouped.ToString();
            }
            string trailing = separator + fracPart + suffix;

            if (zeropad)
            {
                int minLeading = spec.MinimumWidth - sign.Length - trailing.Length;
                // Python's floor division: 3 * min_leading // 4 + 1 when grouping.
                int width = spec.ThousandsSep != '\0' ? FloorDiv(3 * minLeading, 4) + 1 : minLeading;
                if (leading.Length < width)
                {
                    leading = leading.PadLeft(width, '0');
                }
            }

            leading = Group(leading, spec.ThousandsSep);

            return Pad(fill, align, spec.MinimumWidth, sign, leading + trailing);
        }

        private static int FloorDiv(int a, int b) => (a / b) - ((a % b != 0 && (a < 0) != (b < 0)) ? 1 : 0);
    }
}
