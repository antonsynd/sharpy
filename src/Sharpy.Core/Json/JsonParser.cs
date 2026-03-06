using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Recursive descent JSON parser. No external dependencies.
    /// </summary>
    internal static class JsonParser
    {
        internal static object? Parse(string json)
        {
            int pos = 0;
            var result = ParseValue(json, ref pos);
            SkipWhitespace(json, ref pos);
            if (pos < json.Length)
            {
                throw new JsonDecodeError("Extra data", json, pos);
            }
            return result;
        }

        private static object? ParseValue(string json, ref int pos)
        {
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length)
            {
                throw new JsonDecodeError("Expecting value", json, pos);
            }

            char c = json[pos];
            switch (c)
            {
                case '{':
                    return ParseObject(json, ref pos);
                case '[':
                    return ParseArray(json, ref pos);
                case '"':
                    return ParseString(json, ref pos);
                case 't':
                case 'f':
                    return ParseBool(json, ref pos);
                case 'n':
                    return ParseNull(json, ref pos);
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return ParseNumber(json, ref pos);
                    }
                    throw new JsonDecodeError("Expecting value", json, pos);
            }
        }

        private static Dict<string, object?> ParseObject(string json, ref int pos)
        {
            pos++; // skip '{'
            var dict = new Dict<string, object?>();
            SkipWhitespace(json, ref pos);

            if (pos < json.Length && json[pos] == '}')
            {
                pos++;
                return dict;
            }

            while (true)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length || json[pos] != '"')
                {
                    throw new JsonDecodeError("Expecting property name enclosed in double quotes", json, pos);
                }

                string key = ParseString(json, ref pos);
                SkipWhitespace(json, ref pos);

                if (pos >= json.Length || json[pos] != ':')
                {
                    throw new JsonDecodeError("Expecting ':' delimiter", json, pos);
                }
                pos++; // skip ':'

                object? value = ParseValue(json, ref pos);
                dict[key] = value;

                SkipWhitespace(json, ref pos);
                if (pos >= json.Length)
                {
                    throw new JsonDecodeError("Expecting ',' delimiter", json, pos);
                }

                if (json[pos] == '}')
                {
                    pos++;
                    return dict;
                }

                if (json[pos] != ',')
                {
                    throw new JsonDecodeError("Expecting ',' delimiter", json, pos);
                }
                pos++; // skip ','
            }
        }

        private static List<object?> ParseArray(string json, ref int pos)
        {
            pos++; // skip '['
            var list = new List<object?>();
            SkipWhitespace(json, ref pos);

            if (pos < json.Length && json[pos] == ']')
            {
                pos++;
                return list;
            }

            while (true)
            {
                object? value = ParseValue(json, ref pos);
                list.Append(value);

                SkipWhitespace(json, ref pos);
                if (pos >= json.Length)
                {
                    throw new JsonDecodeError("Expecting ',' delimiter", json, pos);
                }

                if (json[pos] == ']')
                {
                    pos++;
                    return list;
                }

                if (json[pos] != ',')
                {
                    throw new JsonDecodeError("Expecting ',' delimiter", json, pos);
                }
                pos++; // skip ','
            }
        }

        private static string ParseString(string json, ref int pos)
        {
            pos++; // skip opening '"'
            var sb = new StringBuilder();

            while (pos < json.Length)
            {
                char c = json[pos];
                if (c == '"')
                {
                    pos++;
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    pos++;
                    if (pos >= json.Length)
                    {
                        throw new JsonDecodeError("Invalid \\escape", json, pos - 1);
                    }

                    char esc = json[pos];
                    switch (esc)
                    {
                        case '"':
                            sb.Append('"');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                        case '/':
                            sb.Append('/');
                            break;
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'u':
                            pos++;
                            int cp = ParseUnicodeEscape(json, ref pos);
                            // Handle surrogate pairs
                            if (cp >= 0xD800 && cp <= 0xDBFF)
                            {
                                if (pos + 1 < json.Length && json[pos] == '\\' && json[pos + 1] == 'u')
                                {
                                    pos += 2; // skip \u
                                    int low = ParseUnicodeEscape(json, ref pos);
                                    if (low >= 0xDC00 && low <= 0xDFFF)
                                    {
                                        int combined = 0x10000 + (cp - 0xD800) * 0x400 + (low - 0xDC00);
                                        sb.Append(char.ConvertFromUtf32(combined));
                                    }
                                    else
                                    {
                                        sb.Append((char)cp);
                                        sb.Append((char)low);
                                    }
                                }
                                else
                                {
                                    sb.Append((char)cp);
                                }
                            }
                            else
                            {
                                sb.Append((char)cp);
                            }
                            continue; // don't increment pos again
                        default:
                            throw new JsonDecodeError("Invalid \\escape: " + esc, json, pos - 1);
                    }
                    pos++;
                }
                else if (c < 0x20)
                {
                    throw new JsonDecodeError("Invalid control character", json, pos);
                }
                else
                {
                    sb.Append(c);
                    pos++;
                }
            }

            throw new JsonDecodeError("Unterminated string starting at", json, pos);
        }

        private static int ParseUnicodeEscape(string json, ref int pos)
        {
            if (pos + 4 > json.Length)
            {
                throw new JsonDecodeError("Invalid \\uXXXX escape", json, pos);
            }

            string hex = json.Substring(pos, 4);
            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint))
            {
                throw new JsonDecodeError("Invalid \\uXXXX escape", json, pos);
            }

            pos += 4;
            return codePoint;
        }

        private static object ParseNumber(string json, ref int pos)
        {
            int start = pos;

            if (pos < json.Length && json[pos] == '-')
                pos++;

            if (pos >= json.Length)
                throw new JsonDecodeError("Expecting value", json, start);

            // Integer part
            if (json[pos] == '0')
            {
                pos++;
            }
            else if (json[pos] >= '1' && json[pos] <= '9')
            {
                while (pos < json.Length && json[pos] >= '0' && json[pos] <= '9')
                    pos++;
            }
            else
            {
                throw new JsonDecodeError("Expecting value", json, start);
            }

            bool isFloat = false;

            // Fractional part
            if (pos < json.Length && json[pos] == '.')
            {
                isFloat = true;
                pos++;
                if (pos >= json.Length || json[pos] < '0' || json[pos] > '9')
                {
                    throw new JsonDecodeError("Expecting digit after decimal point", json, pos);
                }
                while (pos < json.Length && json[pos] >= '0' && json[pos] <= '9')
                    pos++;
            }

            // Exponent part
            if (pos < json.Length && (json[pos] == 'e' || json[pos] == 'E'))
            {
                isFloat = true;
                pos++;
                if (pos < json.Length && (json[pos] == '+' || json[pos] == '-'))
                    pos++;
                if (pos >= json.Length || json[pos] < '0' || json[pos] > '9')
                {
                    throw new JsonDecodeError("Expecting digit in exponent", json, pos);
                }
                while (pos < json.Length && json[pos] >= '0' && json[pos] <= '9')
                    pos++;
            }

            string numStr = json.Substring(start, pos - start);

            if (isFloat)
            {
                if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    return d;
                }
                throw new JsonDecodeError("Invalid number", json, start);
            }
            else
            {
                if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                {
                    return i;
                }
                if (long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                {
                    return l;
                }
                // Fallback to double for very large integers
                if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    return d;
                }
                throw new JsonDecodeError("Invalid number", json, start);
            }
        }

        private static bool ParseBool(string json, ref int pos)
        {
            if (json.Length - pos >= 4 && json.Substring(pos, 4) == "true")
            {
                pos += 4;
                return true;
            }
            if (json.Length - pos >= 5 && json.Substring(pos, 5) == "false")
            {
                pos += 5;
                return false;
            }
            throw new JsonDecodeError("Expecting value", json, pos);
        }

        private static object? ParseNull(string json, ref int pos)
        {
            if (json.Length - pos >= 4 && json.Substring(pos, 4) == "null")
            {
                pos += 4;
                return null;
            }
            throw new JsonDecodeError("Expecting value", json, pos);
        }

        private static void SkipWhitespace(string json, ref int pos)
        {
            while (pos < json.Length)
            {
                char c = json[pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    pos++;
                else
                    break;
            }
        }
    }
}
