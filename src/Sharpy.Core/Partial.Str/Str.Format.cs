using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Format, maketrans, translate, and encode methods for Str.
    /// </summary>
    public readonly partial struct Str
    {
        /// <summary>
        /// Return a formatted version of the string, using positional arguments.
        /// Python: <c>str.format(*args)</c>
        /// </summary>
        public Str Format(params object[] args)
        {
            return new Str(FormatInternal(Value, args, false, null!));
        }

        /// <summary>
        /// Return a formatted version of the string, using a mapping of keyword arguments.
        /// Python: <c>str.format_map(mapping)</c>
        /// </summary>
        public Str Formatmap(Dict<Str, object> mapping)
        {
            return new Str(FormatInternal(Value, null!, true, mapping));
        }

        private static string FormatInternal(string template, object[] args, bool useMapping, Dict<Str, object> mapping)
        {
            var sb = new StringBuilder(template.Length);
            int autoIndex = 0;
            bool usedAutoNumbering = false;
            bool usedManualNumbering = false;
            int i = 0;

            while (i < template.Length)
            {
                char c = template[i];

                if (c == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        sb.Append('{');
                        i += 2;
                        continue;
                    }

                    int closeBrace = template.IndexOf('}', i + 1);
                    if (closeBrace == -1)
                    {
                        throw new ValueError("Single '{' encountered in format string");
                    }

                    string field = template.Substring(i + 1, closeBrace - i - 1);
                    i = closeBrace + 1;

                    string fieldName;
                    string? formatSpec;
                    int colonPos = field.IndexOf(':');
                    if (colonPos >= 0)
                    {
                        fieldName = field.Substring(0, colonPos);
                        formatSpec = field.Substring(colonPos + 1);
                    }
                    else
                    {
                        fieldName = field;
                        formatSpec = null;
                    }

                    object value;

                    if (useMapping)
                    {
                        // format_map mode: look up by name
                        var key = new Str(fieldName);
                        try
                        {
                            value = mapping[key];
                        }
                        catch (KeyError)
                        {
                            throw new KeyError(fieldName);
                        }
                    }
                    else
                    {
                        // format mode: positional
                        int index;
                        if (fieldName.Length == 0)
                        {
                            if (usedManualNumbering)
                            {
                                throw new ValueError(
                                    "cannot switch from manual field specification to automatic field numbering");
                            }
                            usedAutoNumbering = true;
                            index = autoIndex++;
                        }
                        else if (int.TryParse(fieldName, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
                        {
                            if (usedAutoNumbering)
                            {
                                throw new ValueError(
                                    "cannot switch from automatic field numbering to manual field specification");
                            }
                            usedManualNumbering = true;
                            index = parsed;
                        }
                        else
                        {
                            throw new ValueError("cannot use keyword arguments with format(), use format_map()");
                        }

                        if (args == null || index < 0 || index >= args.Length)
                        {
                            throw new IndexError(
                                "Replacement index " + index + " out of range for positional args tuple");
                        }
                        value = args[index];
                    }

                    if (formatSpec != null)
                    {
                        sb.Append(ApplyFormatSpec(value, formatSpec));
                    }
                    else
                    {
                        sb.Append(value);
                    }
                }
                else if (c == '}')
                {
                    if (i + 1 < template.Length && template[i + 1] == '}')
                    {
                        sb.Append('}');
                        i += 2;
                        continue;
                    }
                    throw new ValueError("Single '}' encountered in format string");
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return sb.ToString();
        }

        private static string ApplyFormatSpec(object value, string spec)
        {
            // Parse: [[fill]align][sign][#][0][width][grouping][.precision][type]
            int pos = 0;
            char fill = ' ';
            char align = '\0';
            char sign = '\0';
            bool altForm = false;
            int width = 0;
            char grouping = '\0';
            int precision = -1;
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
                precision = int.Parse(spec.Substring(precStart, pos - precStart), CultureInfo.InvariantCulture);
            }

            // Parse type
            if (pos < spec.Length)
            {
                type = spec[pos];
            }

            // Format the value
            string formatted = FormatValue(value, type, precision, altForm, sign, grouping);

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
                || value is decimal || value is short || value is byte;
        }

        private static string FormatValue(object value, char type, int precision, bool altForm, char sign, char grouping)
        {
            string result;

            switch (type)
            {
                case 'd':
                    result = FormatInteger(value);
                    break;
                case 'f':
                case 'F':
                    result = FormatFloat(value, precision >= 0 ? precision : 6);
                    break;
                case 'e':
                    result = NormalizeScientific(ToDouble(value).ToString(
                        "e" + (precision >= 0 ? precision : 6).ToString(CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture));
                    break;
                case 'E':
                    result = NormalizeScientific(ToDouble(value).ToString(
                        "E" + (precision >= 0 ? precision : 6).ToString(CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture));
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
                case '%':
                    int pctPrec = precision >= 0 ? precision : 6;
                    result = (ToDouble(value) * 100.0).ToString(
                        "F" + pctPrec.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%";
                    break;
                case 's':
                case '\0':
                    if (type == '\0' && IsNumericValue(value))
                    {
                        // No type specified for a number — use default numeric formatting
                        if (value is double || value is float || value is decimal)
                        {
                            if (precision >= 0)
                            {
                                result = FormatFloat(value, precision);
                            }
                            else
                            {
                                result = value.ToString();
                            }
                        }
                        else
                        {
                            result = FormatInteger(value);
                        }
                    }
                    else
                    {
                        result = value != null ? value.ToString() : "";
                        if (precision >= 0 && result.Length > precision)
                        {
                            result = result.Substring(0, precision);
                        }
                    }
                    break;
                default:
                    throw new ValueError("Unknown format code '" + type + "'");
            }

            // Apply sign
            if (sign != '\0' && IsNumericValue(value) && type != '%')
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
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static double ToDouble(object value)
        {
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
            var sb = new StringBuilder();
            int count = 0;
            for (int i = intPart.Length - 1; i >= 0; i--)
            {
                if (count > 0 && count % 3 == 0)
                {
                    sb.Insert(0, separator);
                }
                sb.Insert(0, intPart[i]);
                count++;
            }

            return signPart + sb.ToString() + rest;
        }
        /// <summary>
        /// Build a translation table mapping characters in <paramref name="x"/>
        /// to corresponding characters in <paramref name="y"/>.
        /// Python: <c>str.maketrans(x, y)</c>
        /// </summary>
        public static Dictionary<char, string> Maketrans(string x, string y)
        {
            if (x.Length != y.Length)
            {
                throw new ValueError("the first two maketrans arguments must have equal length");
            }
            var table = new Dictionary<char, string>(x.Length);
            for (int i = 0; i < x.Length; i++)
            {
                table[x[i]] = y[i].ToString();
            }
            return table;
        }

        /// <summary>
        /// Build a translation table with a deletion set.
        /// Python: <c>str.maketrans(x, y, z)</c>
        /// </summary>
        public static Dictionary<char, string> Maketrans(string x, string y, string z)
        {
            var table = Maketrans(x, y);
            foreach (char c in z)
            {
                table[c] = "";
            }
            return table;
        }

        /// <summary>
        /// Return a copy of the string in which each character has been mapped
        /// through the given translation table.
        /// Python: <c>str.translate(table)</c>
        /// </summary>
        public Str Translate(Dictionary<char, string> table)
        {
            var sb = new StringBuilder(Value.Length);
            foreach (char c in Value)
            {
                if (table.TryGetValue(c, out var replacement))
                {
                    sb.Append(replacement);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return new Str(sb.ToString());
        }

        /// <summary>
        /// Encode the string using the specified encoding and return as bytes.
        /// Python: <c>str.encode(encoding='utf-8')</c>
        /// </summary>
        public Bytes Encode(string encoding = "utf-8")
        {
#pragma warning disable CA1307 // string.Replace(string, string, StringComparison) not available in netstandard2.0
            switch (encoding.ToLowerInvariant().Replace("-", "").Replace("_", ""))
#pragma warning restore CA1307
            {
                case "utf8":
                    return new Bytes(Encoding.UTF8.GetBytes(Value));
                case "ascii":
                    return new Bytes(Encoding.ASCII.GetBytes(Value));
                case "utf16":
                case "utf16le":
                    return new Bytes(Encoding.Unicode.GetBytes(Value));
                case "utf16be":
                    return new Bytes(Encoding.BigEndianUnicode.GetBytes(Value));
                case "utf32":
                    return new Bytes(Encoding.UTF32.GetBytes(Value));
                case "latin1":
                case "iso88591":
                    return new Bytes(Encoding.GetEncoding("iso-8859-1").GetBytes(Value));
                default:
                    throw new LookupError("unknown encoding: " + encoding);
            }
        }
    }
}
