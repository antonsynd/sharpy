using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Minimal recursive descent JSON parser.
    /// Returns Dict&lt;string, object&gt; for objects, List&lt;object&gt; for arrays,
    /// string, double (or int/long when no fractional part), bool, or null.
    /// </summary>
    internal static class JsonParser
    {
        public static object? Parse(string json)
        {
            if (json == null)
            {
                throw new TypeError("the JSON object must be str, not NoneType");
            }

            int index = 0;
            object? result = ParseValue(json, ref index);
            SkipWhitespace(json, ref index);
            if (index < json.Length)
            {
                throw new JSONDecodeError(
                    "Extra data",
                    json,
                    index);
            }

            return result;
        }

        private static object? ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);

            if (index >= json.Length)
            {
                throw new JSONDecodeError(
                    "Expecting value",
                    json,
                    index);
            }

            char c = json[index];

            if (c == '"')
            {
                return ParseString(json, ref index);
            }

            if (c == '{')
            {
                return ParseObject(json, ref index);
            }

            if (c == '[')
            {
                return ParseArray(json, ref index);
            }

            if (c == 't')
            {
                return ParseLiteral(json, ref index, "true", true);
            }

            if (c == 'f')
            {
                return ParseLiteral(json, ref index, "false", false);
            }

            if (c == 'n')
            {
                return ParseLiteral(json, ref index, "null", null);
            }

            if (c == '-' || (c >= '0' && c <= '9'))
            {
                return ParseNumber(json, ref index);
            }

            throw new JSONDecodeError(
                "Expecting value",
                json,
                index);
        }

        private static string ParseString(string json, ref int index)
        {
            // Skip opening quote
            index++;

            var sb = new StringBuilder();

            while (index < json.Length)
            {
                char c = json[index];

                if (c == '"')
                {
                    index++;
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    index++;
                    if (index >= json.Length)
                    {
                        throw new JSONDecodeError(
                            "Unterminated string escape",
                            json,
                            index);
                    }

                    char esc = json[index];
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
                            index++;
                            sb.Append(ParseUnicodeEscape(json, ref index));
                            continue; // skip the index++ at end of loop
                        default:
                            throw new JSONDecodeError(
                                "Invalid \\escape: " + esc,
                                json,
                                index);
                    }

                    index++;
                }
                else if (c < ' ')
                {
                    throw new JSONDecodeError(
                        "Invalid control character in string",
                        json,
                        index);
                }
                else
                {
                    sb.Append(c);
                    index++;
                }
            }

            throw new JSONDecodeError(
                "Unterminated string",
                json,
                index);
        }

        private static char ParseUnicodeEscape(string json, ref int index)
        {
            if (index + 4 > json.Length)
            {
                throw new JSONDecodeError(
                    "Invalid \\uXXXX escape",
                    json,
                    index);
            }

            string hex = json.Substring(index, 4);

            if (!int.TryParse(hex.AsSpan(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint))
            {
                throw new JSONDecodeError(
                    "Invalid \\uXXXX escape",
                    json,
                    index);
            }

            index += 4;
            return (char)codePoint;
        }

        private static object ParseNumber(string json, ref int index)
        {
            int start = index;

            // Optional negative sign
            if (index < json.Length && json[index] == '-')
            {
                index++;
            }

            // Integer part
            if (index >= json.Length)
            {
                throw new JSONDecodeError("Expecting value", json, start);
            }

            if (json[index] == '0')
            {
                index++;
            }
            else if (json[index] >= '1' && json[index] <= '9')
            {
                index++;
                while (index < json.Length && json[index] >= '0' && json[index] <= '9')
                {
                    index++;
                }
            }
            else
            {
                throw new JSONDecodeError("Expecting value", json, start);
            }

            bool isFloat = false;

            // Fractional part
            if (index < json.Length && json[index] == '.')
            {
                isFloat = true;
                index++;
                if (index >= json.Length || json[index] < '0' || json[index] > '9')
                {
                    throw new JSONDecodeError("Invalid number", json, start);
                }

                while (index < json.Length && json[index] >= '0' && json[index] <= '9')
                {
                    index++;
                }
            }

            // Exponent part
            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                isFloat = true;
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                {
                    index++;
                }

                if (index >= json.Length || json[index] < '0' || json[index] > '9')
                {
                    throw new JSONDecodeError("Invalid number", json, start);
                }

                while (index < json.Length && json[index] >= '0' && json[index] <= '9')
                {
                    index++;
                }
            }

            string numStr = json.Substring(start, index - start);

            if (isFloat)
            {
                if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    return d;
                }

                throw new JSONDecodeError("Invalid number: " + numStr, json, start);
            }

            // Try int first, then long, then fall back to double
            if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            {
                return i;
            }

            if (long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
            {
                return l;
            }

            if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double dbl))
            {
                return dbl;
            }

            throw new JSONDecodeError("Invalid number: " + numStr, json, start);
        }

        private static Dict<string, object?> ParseObject(string json, ref int index)
        {
            // Skip opening brace
            index++;
            SkipWhitespace(json, ref index);

            var dict = new Dict<string, object?>();

            if (index < json.Length && json[index] == '}')
            {
                index++;
                return dict;
            }

            while (true)
            {
                SkipWhitespace(json, ref index);

                if (index >= json.Length || json[index] != '"')
                {
                    throw new JSONDecodeError(
                        "Expecting property name enclosed in double quotes",
                        json,
                        index);
                }

                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);

                if (index >= json.Length || json[index] != ':')
                {
                    throw new JSONDecodeError(
                        "Expecting ':' delimiter",
                        json,
                        index);
                }

                index++; // skip colon
                SkipWhitespace(json, ref index);

                object? value = ParseValue(json, ref index);
                dict[key] = value;

                SkipWhitespace(json, ref index);

                if (index >= json.Length)
                {
                    throw new JSONDecodeError(
                        "Expecting ',' delimiter or '}'",
                        json,
                        index);
                }

                if (json[index] == '}')
                {
                    index++;
                    return dict;
                }

                if (json[index] != ',')
                {
                    throw new JSONDecodeError(
                        "Expecting ',' delimiter or '}'",
                        json,
                        index);
                }

                index++; // skip comma
            }
        }

        private static List<object?> ParseArray(string json, ref int index)
        {
            // Skip opening bracket
            index++;
            SkipWhitespace(json, ref index);

            var list = new List<object?>();

            if (index < json.Length && json[index] == ']')
            {
                index++;
                return list;
            }

            while (true)
            {
                SkipWhitespace(json, ref index);
                object? value = ParseValue(json, ref index);
                list.Append(value);

                SkipWhitespace(json, ref index);

                if (index >= json.Length)
                {
                    throw new JSONDecodeError(
                        "Expecting ',' delimiter or ']'",
                        json,
                        index);
                }

                if (json[index] == ']')
                {
                    index++;
                    return list;
                }

                if (json[index] != ',')
                {
                    throw new JSONDecodeError(
                        "Expecting ',' delimiter or ']'",
                        json,
                        index);
                }

                index++; // skip comma
            }
        }

        private static object? ParseLiteral(string json, ref int index, string literal, object? value)
        {
            if (index + literal.Length > json.Length ||
                json.Substring(index, literal.Length) != literal)
            {
                throw new JSONDecodeError(
                    "Expecting value",
                    json,
                    index);
            }

            index += literal.Length;
            return value;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                char c = json[index];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    index++;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
