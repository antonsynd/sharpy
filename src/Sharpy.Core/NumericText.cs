using System;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// CPython's numeric-text grammar, shared by <c>int()</c>, every sized integer conversion,
    /// <c>int.parse()</c>, <c>float()</c> and <c>float.parse()</c> (#2357): surrounding
    /// whitespace, an optional sign, a base prefix matching an explicit base, PEP 515 single
    /// underscores between digits, and Unicode decimal digits.
    /// </summary>
    internal static class NumericText
    {
        internal enum Status
        {
            Ok,
            Invalid,
            Overflow,
        }

        // Py_ISSPACE: what CPython strips once the text has been transformed to ASCII.
        private static readonly char[] AsciiSpace = { ' ', '\t', '\n', '\v', '\f', '\r' };

        /// <summary>
        /// CPython's <c>_PyUnicode_TransformDecimalAndSpaceToASCII</c> followed by the strip:
        /// a code point below U+007F is kept as is, a non-ASCII space becomes <c>' '</c>, a Unicode
        /// decimal digit (category Nd) becomes its ASCII digit, and anything else becomes
        /// <c>'?'</c>, which no grammar accepts. U+001C–U+001F stay themselves, so — as in
        /// CPython — they are whitespace to <c>str.isspace</c> but not to <c>int()</c>.
        /// </summary>
        internal static string Normalize(string s)
        {
            StringBuilder? sb = null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c < '\u007F')
                {
                    sb?.Append(c);
                    continue;
                }

                if (sb == null)
                {
                    sb = new StringBuilder(s.Length);
                    sb.Append(s, 0, i);
                }

                if (StringHelpers.IsPythonSpace(c))
                {
                    sb.Append(' ');
                    continue;
                }

                int digit = CharUnicodeInfo.GetDecimalDigitValue(s, i);
                if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    i++;
                }
                sb.Append(digit >= 0 ? (char)('0' + digit) : '?');
            }

            return (sb?.ToString() ?? s).Trim(AsciiSpace);
        }

        /// <summary>
        /// Parse integer text in <paramref name="base"/> (2, 8, 10 or 16) to a sign and a
        /// magnitude. <see cref="Status.Overflow"/> means well-formed text whose magnitude exceeds
        /// <see cref="ulong.MaxValue"/>.
        /// </summary>
        internal static Status ParseInteger(string s, int @base, out bool negative, out ulong magnitude)
        {
            negative = false;
            magnitude = 0;
            if (s == null)
            {
                return Status.Invalid;
            }

            string t = Normalize(s);

            int i = 0;
            if (i < t.Length && (t[i] == '+' || t[i] == '-'))
            {
                negative = t[i] == '-';
                i++;
            }

            // A prefix is accepted only when it names the explicit base; one underscore may follow
            // it ("0x_ff"), since PEP 515 treats the prefix as a digit group.
            if (i + 1 < t.Length && t[i] == '0' && IsPrefixOf(t[i + 1], @base))
            {
                i += 2;
                if (i < t.Length && t[i] == '_')
                {
                    i++;
                }
            }

            bool afterDigit = false;
            bool overflow = false;
            for (; i < t.Length; i++)
            {
                char c = t[i];
                if (c == '_')
                {
                    if (!afterDigit)
                    {
                        return Status.Invalid;
                    }
                    afterDigit = false;
                    continue;
                }

                int d = DigitValue(c);
                if (d < 0 || d >= @base)
                {
                    return Status.Invalid;
                }

                afterDigit = true;
                if (!overflow)
                {
                    if (magnitude > (ulong.MaxValue - (ulong)d) / (ulong)@base)
                    {
                        overflow = true;
                    }
                    else
                    {
                        magnitude = magnitude * (ulong)@base + (ulong)d;
                    }
                }
            }

            // No digits at all, or a trailing underscore.
            if (!afterDigit)
            {
                return Status.Invalid;
            }

            return overflow ? Status.Overflow : Status.Ok;
        }

        /// <summary>
        /// Parse integer text into the signed range [<paramref name="min"/>, <paramref name="max"/>]:
        /// malformed text is <see cref="ValueError"/>, well-formed text outside the range is
        /// <see cref="OverflowError"/> (R-IS — a 32-bit <c>int</c> is the decided deviation; the
        /// exception TYPE is the ruled part).
        /// </summary>
        internal static long ToSigned(string s, int @base, long min, long max, string funcName)
        {
            ValidateBase(@base, funcName);
            var status = ParseInteger(s, @base, out bool negative, out ulong magnitude);
            if (status == Status.Invalid)
            {
                throw InvalidLiteral(s, @base, funcName);
            }

            if (status == Status.Ok)
            {
                if (!negative && magnitude <= (ulong)max)
                {
                    return (long)magnitude;
                }
                if (negative && magnitude <= (ulong)(-(min + 1)) + 1)
                {
                    return unchecked((long)(0UL - magnitude));
                }
            }

            throw OutOfRange(status, negative, magnitude, funcName);
        }

        /// <summary>
        /// Parse integer text into the unsigned range [0, <paramref name="max"/>]; <c>-0</c> is 0.
        /// </summary>
        internal static ulong ToUnsigned(string s, int @base, ulong max, string funcName)
        {
            ValidateBase(@base, funcName);
            var status = ParseInteger(s, @base, out bool negative, out ulong magnitude);
            if (status == Status.Invalid)
            {
                throw InvalidLiteral(s, @base, funcName);
            }

            if (status == Status.Ok && magnitude <= max && (!negative || magnitude == 0))
            {
                return magnitude;
            }

            throw OutOfRange(status, negative, magnitude, funcName);
        }

        /// <summary>
        /// The <c>int.parse()</c> shape: the 32-bit value, or the <see cref="ValueError"/> it is
        /// typed to return. Out-of-range text says so instead of calling a well-formed literal
        /// invalid.
        /// </summary>
        internal static Result<int, ValueError> TryToInt32(string s)
        {
            var status = ParseInteger(s, 10, out bool negative, out ulong magnitude);
            if (status == Status.Invalid)
            {
                return Result<int, ValueError>.Err(InvalidLiteral(s, 10, "int"));
            }

            if (status == Status.Ok)
            {
                if (!negative && magnitude <= int.MaxValue)
                {
                    return Result<int, ValueError>.Ok((int)magnitude);
                }
                if (negative && magnitude <= 2147483648UL)
                {
                    return Result<int, ValueError>.Ok(unchecked((int)(0L - (long)magnitude)));
                }
            }

            return Result<int, ValueError>.Err(new ValueError(OutOfRange(status, negative, magnitude, "int").Message));
        }

        /// <summary>
        /// Parse float text: CPython's special spellings, then PEP 515 underscores (each one
        /// between two digits) removed, then the invariant-culture <c>double</c> grammar.
        /// </summary>
        internal static bool TryParseFloat(string s, out double value)
        {
            if (s == null)
            {
                value = 0.0;
                return false;
            }

            string t = Normalize(s);
            if (DoubleParse.TryParseSpecialFloat(t, out value))
            {
                return true;
            }

            if (t.IndexOf('_') >= 0)
            {
                var sb = new StringBuilder(t.Length);
                for (int i = 0; i < t.Length; i++)
                {
                    if (t[i] != '_')
                    {
                        sb.Append(t[i]);
                    }
                    else if (i == 0 || i == t.Length - 1 || !IsAsciiDigit(t[i - 1]) || !IsAsciiDigit(t[i + 1]))
                    {
                        value = 0.0;
                        return false;
                    }
                }
                t = sb.ToString();
            }

            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static void ValidateBase(int @base, string funcName)
        {
            if (@base != 2 && @base != 8 && @base != 10 && @base != 16)
            {
                throw new ValueError($"{funcName}() base must be 2, 8, 10, or 16, not {@base}");
            }
        }

        private static ValueError InvalidLiteral(string s, int @base, string funcName) =>
            new ValueError($"invalid literal for {funcName}() with base {@base}: '{s}'");

        private static OverflowError OutOfRange(Status status, bool negative, ulong magnitude, string funcName) =>
            status == Status.Overflow
                ? new OverflowError($"int too large to convert to {funcName}")
                : new OverflowError($"Value {(negative ? "-" : "")}{magnitude} is out of range for {funcName}");

        private static bool IsPrefixOf(char c, int @base) =>
            (@base == 16 && (c == 'x' || c == 'X'))
            || (@base == 8 && (c == 'o' || c == 'O'))
            || (@base == 2 && (c == 'b' || c == 'B'));

        private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

        private static int DigitValue(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'z')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'Z')
                return c - 'A' + 10;
            return -1;
        }
    }
}
