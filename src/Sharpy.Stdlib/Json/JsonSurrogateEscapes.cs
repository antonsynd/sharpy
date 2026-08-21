namespace Sharpy
{
    /// <summary>
    /// Single authority for detecting unpaired UTF-16 surrogate escapes in JSON text.
    /// Both <c>json.loads</c> (untyped) and <c>json.loads[T]</c> (typed) consult this
    /// BEFORE parsing, so they refuse the same documents with the same message (#1487).
    /// </summary>
    internal static class JsonSurrogateEscapes
    {
        /// <summary>
        /// Scans <paramref name="json"/> for the first unpaired <c>\uXXXX</c> surrogate escape.
        /// Returns the escape text and its character position, or <c>null</c> when every
        /// surrogate escape is properly paired.
        /// </summary>
        /// <remarks>
        /// The scan is string-aware: a <c>\uXXXX</c> outside a JSON string literal is ignored
        /// (the parser rejects it separately), and an escaped backslash (<c>\\</c>) does not
        /// start an escape sequence. Hex digits are matched case-insensitively.
        /// </remarks>
        internal static (string escapeText, int position)? FirstUnpaired(string json)
        {
            bool inString = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    if (c == '\\' && i + 1 < json.Length)
                    {
                        char next = json[i + 1];
                        if (next == 'u' || next == 'U')
                        {
                            int escapeStart = i;
                            if (i + 5 < json.Length && TryParseHex4(json, i + 2, out int codeUnit))
                            {
                                if (IsHighSurrogate(codeUnit))
                                {
                                    if (HasFollowingLowSurrogateEscape(json, i + 6, out int lowUnit))
                                    {
                                        // Valid pair — skip both escapes
                                        i += 11; // past \uXXXX\uXXXX (minus the loop increment)
                                        continue;
                                    }

                                    return (json.Substring(escapeStart, 6), escapeStart);
                                }

                                if (IsLowSurrogate(codeUnit))
                                {
                                    return (json.Substring(escapeStart, 6), escapeStart);
                                }

                                // Non-surrogate \uXXXX — skip it
                                i += 5;
                                continue;
                            }

                            // Malformed \uXXXX — not our concern, parser handles it
                            i++;
                            continue;
                        }

                        // Any other escape (\\, \", \n, etc.) — skip the escaped char
                        i++;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
            }

            return null;
        }

        private static bool TryParseHex4(string s, int offset, out int value)
        {
            value = 0;
            if (offset + 4 > s.Length)
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                int digit = HexDigit(s[offset + i]);
                if (digit < 0)
                {
                    return false;
                }
                value = (value << 4) | digit;
            }

            return true;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }

        private static bool IsHighSurrogate(int codeUnit)
        {
            return codeUnit >= 0xD800 && codeUnit <= 0xDBFF;
        }

        private static bool IsLowSurrogate(int codeUnit)
        {
            return codeUnit >= 0xDC00 && codeUnit <= 0xDFFF;
        }

        private static bool HasFollowingLowSurrogateEscape(string json, int offset, out int lowUnit)
        {
            lowUnit = 0;
            if (offset + 6 > json.Length)
            {
                return false;
            }

            if (json[offset] != '\\')
            {
                return false;
            }

            char u = json[offset + 1];
            if (u != 'u' && u != 'U')
            {
                return false;
            }

            if (!TryParseHex4(json, offset + 2, out lowUnit))
            {
                return false;
            }

            return IsLowSurrogate(lowUnit);
        }
    }
}
