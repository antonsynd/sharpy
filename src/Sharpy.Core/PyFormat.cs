using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// The one Python format-spec engine. <c>str.format</c>, <c>format()</c>
    /// (<see cref="Builtins.Format(object, string)"/>) and every f-string hole route their
    /// <c>[[fill]align][sign][z][#][0][width][grouping][.precision][type]</c> specs through
    /// <see cref="Apply(object, string)"/> so there is exactly one CPython-conforming implementation.
    /// </summary>
    public static class PyFormat
    {
        /// <summary>
        /// Format <paramref name="value"/> according to the Python format specification
        /// <paramref name="spec"/>. An empty spec yields <c>str(value)</c>; a non-empty spec on
        /// <c>None</c> raises <see cref="TypeError"/>, matching CPython.
        /// </summary>
        public static string Apply(object? value, string spec)
        {
            if (string.IsNullOrEmpty(spec))
            {
                // Empty spec == str(value): today's behavior, None spelled "None".
                return value == null ? "None" : (value.ToString() ?? "None");
            }

            if (value == null)
            {
                throw new TypeError("unsupported format string passed to NoneType.__format__");
            }

            return ApplyFormatSpec(value, spec);
        }

        private static string ApplyFormatSpec(object value, string spec)
        {
            // Parse: [[fill]align][sign][z][#][0][width][grouping][.precision][type]
            int pos = 0;
            char fill = ' ';
            char align = '\0';
            char sign = '\0';
            bool zCoerce = false;
            bool altForm = false;
            int width = 0;
            char grouping = '\0';
            int precision = -1;
            bool hasPrecision = false;
            char type = '\0';

            // Parse fill and align
            if (spec.Length >= 2 && IsAlign(spec[1]))
            {
                fill = spec[0];
                align = spec[1];
                pos = 2;
            }
            else if (spec.Length >= 1 && IsAlign(spec[0]))
            {
                align = spec[0];
                pos = 1;
            }

            // Parse sign
            if (pos < spec.Length && (spec[pos] == '+' || spec[pos] == '-' || spec[pos] == ' '))
            {
                sign = spec[pos];
                pos++;
            }

            // Parse z (PEP 682 negative-zero coercion) — after the sign, before '#'
            if (pos < spec.Length && spec[pos] == 'z')
            {
                zCoerce = true;
                pos++;
            }

            // Parse # (alternate form)
            if (pos < spec.Length && spec[pos] == '#')
            {
                altForm = true;
                pos++;
            }

            // Parse 0 (zero padding)
            if (pos < spec.Length && spec[pos] == '0')
            {
                if (align == '\0')
                {
                    align = '=';
                    fill = '0';
                }
                pos++;
            }

            // Parse width
            int widthStart = pos;
            while (pos < spec.Length && spec[pos] >= '0' && spec[pos] <= '9')
            {
                pos++;
            }
            if (pos > widthStart)
            {
                width = int.Parse(spec.Substring(widthStart, pos - widthStart), CultureInfo.InvariantCulture);
            }

            // Parse grouping (, or _)
            if (pos < spec.Length && (spec[pos] == ',' || spec[pos] == '_'))
            {
                grouping = spec[pos];
                pos++;
            }

            // Parse precision
            if (pos < spec.Length && spec[pos] == '.')
            {
                pos++;
                int precStart = pos;
                while (pos < spec.Length && spec[pos] >= '0' && spec[pos] <= '9')
                {
                    pos++;
                }
                precision = precStart == pos
                    ? 0
                    : int.Parse(spec.Substring(precStart, pos - precStart), CultureInfo.InvariantCulture);
                hasPrecision = true;
            }

            // Parse type — exactly one trailing character; anything left over is invalid.
            if (pos < spec.Length)
            {
                type = spec[pos];
                pos++;
            }
            if (pos != spec.Length)
            {
                throw new ValueError(
                    "Invalid format specifier '" + spec + "' for object of type '" + PyTypeName(value) + "'");
            }

            // Format the value
            string formatted = FormatValue(value, type, precision, hasPrecision, altForm, sign, grouping, zCoerce);

            // Apply width and alignment
            if (width > 0 && formatted.Length < width)
            {
                if (align == '\0')
                {
                    // Default: numbers right-align, strings left-align
                    align = IsNumericValue(value) ? '>' : '<';
                }

                int padding = width - formatted.Length;
                switch (align)
                {
                    case '<':
                        formatted = formatted + new string(fill, padding);
                        break;
                    case '>':
                        formatted = new string(fill, padding) + formatted;
                        break;
                    case '^':
                        int left = padding / 2;
                        int right = padding - left;
                        formatted = new string(fill, left) + formatted + new string(fill, right);
                        break;
                    case '=':
                        // Padding between sign and digits
                        if (formatted.Length > 0 && (formatted[0] == '+' || formatted[0] == '-' || formatted[0] == ' '))
                        {
                            formatted = formatted[0] + new string(fill, padding) + formatted.Substring(1);
                        }
                        else
                        {
                            formatted = new string(fill, padding) + formatted;
                        }
                        break;
                }
            }

            return formatted;
        }

        private static bool IsAlign(char c)
        {
            return c == '<' || c == '>' || c == '^' || c == '=';
        }

        private static bool IsNumericValue(object value)
        {
            return value is int || value is long || value is double || value is float
                || value is decimal || value is short || value is byte || value is bool;
        }

        /// <summary>Python type name used in format-error messages.</summary>
        private static string PyTypeName(object value)
        {
            if (value is bool)
            {
                return "bool";
            }
            if (value is double || value is float || value is decimal)
            {
                return "float";
            }
            if (value is int || value is long || value is short || value is byte
                || value is sbyte || value is uint || value is ulong || value is ushort)
            {
                return "int";
            }
            if (value is string)
            {
                return "str";
            }
            return value.GetType().Name;
        }

        private static string FormatValue(object value, char type, int precision, bool hasPrecision,
            bool altForm, char sign, char grouping, bool zCoerce)
        {
            bool isBool = value is bool;
            bool isFloat = value is double || value is float || value is decimal;
            bool isIntegral = isBool || value is int || value is long || value is short || value is byte
                || value is sbyte || value is uint || value is ulong || value is ushort;
            bool isStr = value is string;

            // Enforce the CPython type-code validity matrix before formatting.
            if (isStr)
            {
                if (type != '\0' && type != 's')
                {
                    throw new ValueError("Unknown format code '" + type + "' for object of type 'str'");
                }
            }
            else if (isFloat)
            {
                if (type == 'b' || type == 'c' || type == 'd' || type == 'o'
                    || type == 'x' || type == 'X' || type == 's')
                {
                    throw new ValueError(
                        "Unknown format code '" + type + "' for object of type 'float'");
                }
            }
            else if (isIntegral)
            {
                if (type == 's')
                {
                    throw new ValueError(
                        "Unknown format code 's' for object of type '" + PyTypeName(value) + "'");
                }
                bool integerPresentation = type == '\0' || type == 'b' || type == 'c' || type == 'd'
                    || type == 'n' || type == 'o' || type == 'x' || type == 'X';
                if (hasPrecision && integerPresentation)
                {
                    throw new ValueError("Precision not allowed in integer format specifier");
                }
                if (zCoerce && integerPresentation)
                {
                    throw new ValueError("Negative zero coercion (z) not allowed in integer format specifier");
                }
            }

            string result;

            switch (type)
            {
                case 'd':
                    result = FormatInteger(value);
                    break;
                case 'n':
                    // Locale-aware in CPython; we use the invariant locale, so 'n' == 'd'/'g'.
                    result = isFloat
                        ? FormatGeneral(value, precision, hasPrecision, false)
                        : FormatInteger(value);
                    break;
                case 'f':
                case 'F':
                    result = FormatFloat(value, hasPrecision ? precision : 6);
                    break;
                case 'e':
                    result = NormalizeScientific(ToDouble(value).ToString(
                        "e" + (hasPrecision ? precision : 6).ToString(CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture));
                    break;
                case 'E':
                    result = NormalizeScientific(ToDouble(value).ToString(
                        "E" + (hasPrecision ? precision : 6).ToString(CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture));
                    break;
                case 'g':
                    result = FormatGeneral(value, precision, hasPrecision, false);
                    break;
                case 'G':
                    result = FormatGeneral(value, precision, hasPrecision, true);
                    break;
                case 'x':
                    result = ToLong(value).ToString("x", CultureInfo.InvariantCulture);
                    if (altForm)
                        result = "0x" + result;
                    break;
                case 'X':
                    result = ToLong(value).ToString("X", CultureInfo.InvariantCulture);
                    if (altForm)
                        result = "0X" + result;
                    break;
                case 'o':
                    result = Convert.ToString(ToLong(value), 8);
                    if (altForm)
                        result = "0o" + result;
                    break;
                case 'b':
                    result = Convert.ToString(ToLong(value), 2);
                    if (altForm)
                        result = "0b" + result;
                    break;
                case 'c':
                    result = char.ConvertFromUtf32((int)ToLong(value));
                    break;
                case '%':
                    int pctPrec = hasPrecision ? precision : 6;
                    result = (ToDouble(value) * 100.0).ToString(
                        "F" + pctPrec.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%";
                    break;
                case 's':
                case '\0':
                    if (type == '\0' && isFloat)
                    {
                        // float with no type: full repr, or significant-digit precision when given.
                        result = hasPrecision
                            ? FormatFloatSignificant(value, precision)
                            : (value.ToString() ?? "");
                    }
                    else if (type == '\0' && isIntegral)
                    {
                        result = FormatInteger(value);
                    }
                    else
                    {
                        result = value.ToString() ?? "";
                        if (hasPrecision && result.Length > precision)
                        {
                            result = result.Substring(0, precision);
                        }
                    }
                    break;
                default:
                    throw new ValueError(
                        "Unknown format code '" + type + "' for object of type '" + PyTypeName(value) + "'");
            }

            // PEP 682: coerce a formatted negative zero to positive zero.
            if (zCoerce && IsNegativeZeroText(result))
            {
                result = result.Substring(1);
            }

            // Apply sign
            if (sign != '\0' && (isIntegral || isFloat) && type != '%' && type != 'c')
            {
                if (result.Length > 0 && result[0] != '-')
                {
                    if (sign == '+')
                    {
                        result = "+" + result;
                    }
                    else if (sign == ' ')
                    {
                        result = " " + result;
                    }
                }
            }

            // Apply grouping
            if (grouping != '\0')
            {
                result = ApplyGrouping(result, grouping);
            }

            return result;
        }

        private static bool IsNegativeZeroText(string s)
        {
            if (s.Length == 0 || s[0] != '-')
            {
                return false;
            }
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '0' && c != '.' && c != ',' && c != '_')
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// CPython's <c>g</c>/<c>G</c> presentation: <paramref name="precision"/> significant digits
        /// (default 6, minimum 1), fixed when the decimal exponent is in <c>[-4, precision)</c> and
        /// scientific otherwise, with trailing zeros stripped.
        /// </summary>
        private static string FormatGeneral(object value, int precision, bool hasPrecision, bool upper)
        {
            double d = ToDouble(value);
            int p = hasPrecision ? (precision == 0 ? 1 : precision) : 6;
            string eForm = upper ? "E" : "e";
            string precStr = (p - 1).ToString(CultureInfo.InvariantCulture);
            int exp = ParseExponent(d.ToString(eForm + precStr, CultureInfo.InvariantCulture));

            if (exp >= -4 && exp < p)
            {
                int frac = p - 1 - exp;
                if (frac < 0)
                {
                    frac = 0;
                }
                string fixedText = d.ToString(
                    "F" + frac.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                return StripFixedZeros(fixedText, false);
            }

            string sci = NormalizeScientific(d.ToString(eForm + precStr, CultureInfo.InvariantCulture));
            return StripSciZeros(sci);
        }

        /// <summary>
        /// CPython's float format with NO type character but a precision: like <c>g</c> but the
        /// switch to scientific happens one exponent earlier (<c>exp &gt;= precision - 1</c>) and a
        /// fixed result always keeps at least one fractional digit.
        /// </summary>
        private static string FormatFloatSignificant(object value, int precision)
        {
            double d = ToDouble(value);
            int p = precision == 0 ? 1 : precision;
            string precStr = (p - 1).ToString(CultureInfo.InvariantCulture);
            int exp = ParseExponent(d.ToString("e" + precStr, CultureInfo.InvariantCulture));

            if (exp >= -4 && exp < p - 1)
            {
                int frac = p - 1 - exp;
                if (frac < 0)
                {
                    frac = 0;
                }
                string fixedText = d.ToString(
                    "F" + frac.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                return StripFixedZeros(fixedText, true);
            }

            string sci = NormalizeScientific(d.ToString("e" + precStr, CultureInfo.InvariantCulture));
            return StripSciZeros(sci);
        }

        private static int ParseExponent(string sci)
        {
            int ePos = sci.IndexOf('e');
            if (ePos < 0)
            {
                ePos = sci.IndexOf('E');
            }
            if (ePos < 0)
            {
                return 0;
            }
            return int.Parse(sci.Substring(ePos + 1), NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static string StripFixedZeros(string s, bool keepOneDecimal)
        {
            if (s.IndexOf('.') < 0)
            {
                return s;
            }
            s = s.TrimEnd('0');
            if (s.EndsWith(".", StringComparison.Ordinal))
            {
                s = keepOneDecimal ? s + "0" : s.Substring(0, s.Length - 1);
            }
            return s;
        }

        private static string StripSciZeros(string s)
        {
            int ePos = s.IndexOf('e');
            if (ePos < 0)
            {
                ePos = s.IndexOf('E');
            }
            if (ePos < 0)
            {
                return StripFixedZeros(s, false);
            }
            string mantissa = StripFixedZeros(s.Substring(0, ePos), false);
            return mantissa + s.Substring(ePos);
        }

        private static string NormalizeScientific(string s)
        {
            // .NET may produce 3-digit exponents (e+000), Python uses 2-digit minimum (e+00)
            int ePos = s.IndexOf('e');
            if (ePos < 0)
                ePos = s.IndexOf('E');
            if (ePos < 0)
                return s;

            string mantissa = s.Substring(0, ePos + 2); // includes e/E and sign
            string exponent = s.Substring(ePos + 2);
            // Remove leading zeros but keep at least 2 digits
            string trimmed = exponent.TrimStart('0');
            if (trimmed.Length < 2)
                trimmed = exponent.Length >= 2 ? exponent.Substring(exponent.Length - 2) : exponent.PadLeft(2, '0');
            return mantissa + trimmed;
        }

        private static string FormatInteger(object value)
        {
            return ToLong(value).ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(object value, int precision)
        {
            return ToDouble(value).ToString(
                "F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static long ToLong(object value)
        {
            if (value is bool b)
            {
                return b ? 1L : 0L;
            }
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static double ToDouble(object value)
        {
            if (value is bool b)
            {
                return b ? 1.0 : 0.0;
            }
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static string ApplyGrouping(string formatted, char separator)
        {
            // Find the integer part (before decimal point or end)
            int signLen = 0;
            if (formatted.Length > 0 && (formatted[0] == '-' || formatted[0] == '+' || formatted[0] == ' '))
            {
                signLen = 1;
            }

            int dotPos = formatted.IndexOf('.');
            string intPart = dotPos >= 0
                ? formatted.Substring(signLen, dotPos - signLen)
                : formatted.Substring(signLen);
            string rest = dotPos >= 0 ? formatted.Substring(dotPos) : "";
            string signPart = signLen > 0 ? formatted.Substring(0, signLen) : "";

            // Insert separators every 3 digits from the right
            var sb = new System.Text.StringBuilder();
            int count = 0;
            for (int idx = intPart.Length - 1; idx >= 0; idx--)
            {
                if (count > 0 && count % 3 == 0)
                {
                    sb.Insert(0, separator);
                }
                sb.Insert(0, intPart[idx]);
                count++;
            }

            return signPart + sb.ToString() + rest;
        }
    }
}
