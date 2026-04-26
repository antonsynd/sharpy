using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Format, maketrans, translate, encode, and case folding extension methods for string.
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// Return a formatted version of the string, using positional arguments.
        /// Python: <c>str.format(*args)</c>
        /// </summary>
        public static string Format(this string s, params object[] args)
        {
            return FormatInternal(s, args, false, null!);
        }

        /// <summary>
        /// Return a formatted version of the string, using a mapping of keyword arguments.
        /// Python: <c>str.format_map(mapping)</c>
        /// </summary>
        public static string FormatMap(this string s, Dict<string, object> mapping)
        {
            return FormatInternal(s, null!, true, mapping);
        }

        private static string FormatInternal(string template, object[] args, bool useMapping, Dict<string, object> mapping)
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
                        try
                        {
                            value = mapping[fieldName];
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
                                result = value.ToString() ?? "";
                            }
                        }
                        else
                        {
                            result = FormatInteger(value);
                        }
                    }
                    else
                    {
                        result = value != null ? (value.ToString() ?? "") : "";
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

            return result!;
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
            for (int idx = 0; idx < x.Length; idx++)
            {
                table[x[idx]] = y[idx].ToString();
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
        public static string Translate(this string s, Dictionary<char, string> table)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
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
            return sb.ToString();
        }

        /// <summary>
        /// Encode the string using the specified encoding and return as bytes.
        /// Python: <c>str.encode(encoding='utf-8')</c>
        /// </summary>
        public static Bytes Encode(this string s, string encoding = "utf-8")
        {
#pragma warning disable CA1307
            switch (encoding.ToLowerInvariant().Replace("-", "").Replace("_", ""))
#pragma warning restore CA1307
            {
                case "utf8":
                    return new Bytes(Encoding.UTF8.GetBytes(s));
                case "ascii":
                    return new Bytes(Encoding.ASCII.GetBytes(s));
                case "utf16":
                case "utf16le":
                    return new Bytes(Encoding.Unicode.GetBytes(s));
                case "utf16be":
                    return new Bytes(Encoding.BigEndianUnicode.GetBytes(s));
                case "utf32":
                    return new Bytes(Encoding.UTF32.GetBytes(s));
                case "latin1":
                case "iso88591":
                    return new Bytes(Encoding.GetEncoding("iso-8859-1").GetBytes(s));
                default:
                    throw new LookupError("unknown encoding: " + encoding);
            }
        }

        // ----------------------------------------------------------------
        // Case Folding
        // ----------------------------------------------------------------

        // Unicode full case folding table (status "F" and "C" entries from CaseFolding.txt)
        // where the result differs from ToLowerInvariant(). Cherokee ranges are handled
        // by range checks in CaseFoldChar() to keep this table compact.
        private static readonly Dictionary<char, string> s_caseFoldTable = new Dictionary<char, string>(125)
        {
            // Latin/Common
            { '\u00B5', "\u03bc" },       // MICRO SIGN -> Greek small mu
            { '\u00DF', "ss" },           // LATIN SMALL LETTER SHARP S
            { '\u0149', "\u02bcn" },      // LATIN SMALL LETTER N PRECEDED BY APOSTROPHE
            { '\u017F', "s" },            // LATIN SMALL LETTER LONG S
            { '\u01F0', "j\u030c" },      // LATIN SMALL LETTER J WITH CARON

            // Greek
            { '\u0345', "\u03b9" },       // COMBINING GREEK YPOGEGRAMMENI -> iota
            { '\u0390', "\u03b9\u0308\u0301" }, // GREEK SMALL LETTER IOTA WITH DIALYTIKA AND TONOS
            { '\u03B0', "\u03c5\u0308\u0301" }, // GREEK SMALL LETTER UPSILON WITH DIALYTIKA AND TONOS
            { '\u03C2', "\u03c3" },       // GREEK SMALL LETTER FINAL SIGMA -> sigma
            { '\u03D0', "\u03b2" },       // GREEK BETA SYMBOL -> beta
            { '\u03D1', "\u03b8" },       // GREEK THETA SYMBOL -> theta
            { '\u03D5', "\u03c6" },       // GREEK PHI SYMBOL -> phi
            { '\u03D6', "\u03c0" },       // GREEK PI SYMBOL -> pi
            { '\u03F0', "\u03ba" },       // GREEK KAPPA SYMBOL -> kappa
            { '\u03F1', "\u03c1" },       // GREEK RHO SYMBOL -> rho
            { '\u03F5', "\u03b5" },       // GREEK LUNATE EPSILON SYMBOL -> epsilon

            // Armenian
            { '\u0587', "\u0565\u0582" }, // ARMENIAN SMALL LIGATURE ECH YIWN

            // Cyrillic
            { '\u1C80', "\u0432" },       // CYRILLIC SMALL LETTER ROUNDED VE
            { '\u1C81', "\u0434" },       // CYRILLIC SMALL LETTER LONG-LEGGED DE
            { '\u1C82', "\u043e" },       // CYRILLIC SMALL LETTER NARROW O
            { '\u1C83', "\u0441" },       // CYRILLIC SMALL LETTER WIDE ES
            { '\u1C84', "\u0442" },       // CYRILLIC SMALL LETTER TALL TE
            { '\u1C85', "\u0442" },       // CYRILLIC SMALL LETTER THREE-LEGGED TE
            { '\u1C86', "\u044a" },       // CYRILLIC SMALL LETTER TALL HARD SIGN
            { '\u1C87', "\u0463" },       // CYRILLIC SMALL LETTER TALL YAT
            { '\u1C88', "\ua64b" },       // CYRILLIC SMALL LETTER UNBLENDED UK

            // Latin Extended Additional
            { '\u1E96', "h\u0331" },      // LATIN SMALL LETTER H WITH LINE BELOW
            { '\u1E97', "t\u0308" },      // LATIN SMALL LETTER T WITH DIAERESIS
            { '\u1E98', "w\u030a" },      // LATIN SMALL LETTER W WITH RING ABOVE
            { '\u1E99', "y\u030a" },      // LATIN SMALL LETTER Y WITH RING ABOVE
            { '\u1E9A', "a\u02be" },      // LATIN SMALL LETTER A WITH RIGHT HALF RING
            { '\u1E9B', "\u1e61" },       // LATIN SMALL LETTER LONG S WITH DOT ABOVE
            { '\u1E9E', "ss" },           // LATIN CAPITAL LETTER SHARP S

            // Greek Extended
            { '\u1F50', "\u03c5\u0313" },
            { '\u1F52', "\u03c5\u0313\u0300" },
            { '\u1F54', "\u03c5\u0313\u0301" },
            { '\u1F56', "\u03c5\u0313\u0342" },
            { '\u1F80', "\u1f00\u03b9" },
            { '\u1F81', "\u1f01\u03b9" },
            { '\u1F82', "\u1f02\u03b9" },
            { '\u1F83', "\u1f03\u03b9" },
            { '\u1F84', "\u1f04\u03b9" },
            { '\u1F85', "\u1f05\u03b9" },
            { '\u1F86', "\u1f06\u03b9" },
            { '\u1F87', "\u1f07\u03b9" },
            { '\u1F88', "\u1f00\u03b9" },
            { '\u1F89', "\u1f01\u03b9" },
            { '\u1F8A', "\u1f02\u03b9" },
            { '\u1F8B', "\u1f03\u03b9" },
            { '\u1F8C', "\u1f04\u03b9" },
            { '\u1F8D', "\u1f05\u03b9" },
            { '\u1F8E', "\u1f06\u03b9" },
            { '\u1F8F', "\u1f07\u03b9" },
            { '\u1F90', "\u1f20\u03b9" },
            { '\u1F91', "\u1f21\u03b9" },
            { '\u1F92', "\u1f22\u03b9" },
            { '\u1F93', "\u1f23\u03b9" },
            { '\u1F94', "\u1f24\u03b9" },
            { '\u1F95', "\u1f25\u03b9" },
            { '\u1F96', "\u1f26\u03b9" },
            { '\u1F97', "\u1f27\u03b9" },
            { '\u1F98', "\u1f20\u03b9" },
            { '\u1F99', "\u1f21\u03b9" },
            { '\u1F9A', "\u1f22\u03b9" },
            { '\u1F9B', "\u1f23\u03b9" },
            { '\u1F9C', "\u1f24\u03b9" },
            { '\u1F9D', "\u1f25\u03b9" },
            { '\u1F9E', "\u1f26\u03b9" },
            { '\u1F9F', "\u1f27\u03b9" },
            { '\u1FA0', "\u1f60\u03b9" },
            { '\u1FA1', "\u1f61\u03b9" },
            { '\u1FA2', "\u1f62\u03b9" },
            { '\u1FA3', "\u1f63\u03b9" },
            { '\u1FA4', "\u1f64\u03b9" },
            { '\u1FA5', "\u1f65\u03b9" },
            { '\u1FA6', "\u1f66\u03b9" },
            { '\u1FA7', "\u1f67\u03b9" },
            { '\u1FA8', "\u1f60\u03b9" },
            { '\u1FA9', "\u1f61\u03b9" },
            { '\u1FAA', "\u1f62\u03b9" },
            { '\u1FAB', "\u1f63\u03b9" },
            { '\u1FAC', "\u1f64\u03b9" },
            { '\u1FAD', "\u1f65\u03b9" },
            { '\u1FAE', "\u1f66\u03b9" },
            { '\u1FAF', "\u1f67\u03b9" },
            { '\u1FB2', "\u1f70\u03b9" },
            { '\u1FB3', "\u03b1\u03b9" },
            { '\u1FB4', "\u03ac\u03b9" },
            { '\u1FB6', "\u03b1\u0342" },
            { '\u1FB7', "\u03b1\u0342\u03b9" },
            { '\u1FBC', "\u03b1\u03b9" },
            { '\u1FBE', "\u03b9" },
            { '\u1FC2', "\u1f74\u03b9" },
            { '\u1FC3', "\u03b7\u03b9" },
            { '\u1FC4', "\u03ae\u03b9" },
            { '\u1FC6', "\u03b7\u0342" },
            { '\u1FC7', "\u03b7\u0342\u03b9" },
            { '\u1FCC', "\u03b7\u03b9" },
            { '\u1FD2', "\u03b9\u0308\u0300" },
            { '\u1FD3', "\u03b9\u0308\u0301" },
            { '\u1FD6', "\u03b9\u0342" },
            { '\u1FD7', "\u03b9\u0308\u0342" },
            { '\u1FE2', "\u03c5\u0308\u0300" },
            { '\u1FE3', "\u03c5\u0308\u0301" },
            { '\u1FE4', "\u03c1\u0313" },
            { '\u1FE6', "\u03c5\u0342" },
            { '\u1FE7', "\u03c5\u0308\u0342" },
            { '\u1FF2', "\u1f7c\u03b9" },
            { '\u1FF3', "\u03c9\u03b9" },
            { '\u1FF4', "\u03ce\u03b9" },
            { '\u1FF6', "\u03c9\u0342" },
            { '\u1FF7', "\u03c9\u0342\u03b9" },
            { '\u1FFC', "\u03c9\u03b9" },

            // Ligatures / Compatibility
            { '\uFB00', "ff" },
            { '\uFB01', "fi" },
            { '\uFB02', "fl" },
            { '\uFB03', "ffi" },
            { '\uFB04', "ffl" },
            { '\uFB05', "st" },
            { '\uFB06', "st" },

            // Armenian ligatures
            { '\uFB13', "\u0574\u0576" },
            { '\uFB14', "\u0574\u0565" },
            { '\uFB15', "\u0574\u056b" },
            { '\uFB16', "\u057e\u0576" },
            { '\uFB17', "\u0574\u056d" },
        };

        private static string CaseFoldChar(char c)
        {
            // Cherokee uppercase U+13A0-U+13F5: casefold is identity (not lowercased)
            if (c >= '\u13A0' && c <= '\u13F5')
            {
                return c.ToString();
            }

            // Cherokee small U+13F8-U+13FD: casefold maps to U+13F0-U+13F5
            if (c >= '\u13F8' && c <= '\u13FD')
            {
                return ((char)(c - 8)).ToString();
            }

            // Cherokee small letter U+AB70-U+ABBF: casefold maps to U+13A0-U+13EF
            if (c >= '\uAB70' && c <= '\uABBF')
            {
                return ((char)(c - 0x97D0)).ToString();
            }

            // Check the folding table for special mappings
            if (s_caseFoldTable.TryGetValue(c, out var folded))
            {
                return folded;
            }

            // Default: use invariant lowercase
            return char.ToLowerInvariant(c).ToString();
        }
    }
}
