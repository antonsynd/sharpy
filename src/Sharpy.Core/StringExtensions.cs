using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Extension methods on <see cref="string"/> that provide Python string method
    /// equivalents under PascalCase names.  The emitter's NameMangler converts
    /// <c>upper</c> to <c>Upper</c>, <c>lower</c> to <c>Lower</c>, etc.
    /// Generated code includes <c>using global::Sharpy;</c> which brings these
    /// extensions into scope so that <c>name.Upper()</c> compiles against C#
    /// <see cref="string"/>.
    /// </summary>
    public static class StringExtensions
    {
        // ----------------------------------------------------------------
        // Priority 1 — core methods
        // ----------------------------------------------------------------

        /// <summary>
        /// Return a copy of the string converted to uppercase.
        /// Python: <c>str.upper()</c>
        /// </summary>
        /// <remarks>Uses invariant culture to match Python's culture-independent behavior.</remarks>
        public static string Upper(this string s)
        {
            return s.ToUpperInvariant();
        }

        /// <summary>
        /// Return a copy of the string converted to lowercase.
        /// Python: <c>str.lower()</c>
        /// </summary>
        /// <remarks>Uses invariant culture to match Python's culture-independent behavior.</remarks>
        public static string Lower(this string s)
        {
            return s.ToLowerInvariant();
        }

        /// <summary>
        /// Return a copy of the string with leading and trailing whitespace removed.
        /// Python: <c>str.strip()</c>
        /// </summary>
        public static string Strip(this string s)
        {
            return s.Trim();
        }

        /// <summary>
        /// Return a copy of the string with leading and trailing characters in
        /// <paramref name="chars"/> removed.
        /// Python: <c>str.strip(chars)</c>
        /// </summary>
        public static string Strip(this string s, string chars)
        {
            return s.Trim(chars.ToCharArray());
        }

        /// <summary>
        /// Return a copy of the string with leading whitespace removed.
        /// Python: <c>str.lstrip()</c>
        /// </summary>
        public static string Lstrip(this string s)
        {
            return s.TrimStart();
        }

        /// <summary>
        /// Return a copy of the string with leading characters in
        /// <paramref name="chars"/> removed.
        /// Python: <c>str.lstrip(chars)</c>
        /// </summary>
        public static string Lstrip(this string s, string chars)
        {
            return s.TrimStart(chars.ToCharArray());
        }

        /// <summary>
        /// Return a copy of the string with trailing whitespace removed.
        /// Python: <c>str.rstrip()</c>
        /// </summary>
        public static string Rstrip(this string s)
        {
            return s.TrimEnd();
        }

        /// <summary>
        /// Return a copy of the string with trailing characters in
        /// <paramref name="chars"/> removed.
        /// Python: <c>str.rstrip(chars)</c>
        /// </summary>
        public static string Rstrip(this string s, string chars)
        {
            return s.TrimEnd(chars.ToCharArray());
        }

        /// <summary>
        /// Return a copy of the string with its first character capitalized
        /// and the rest lowercased.
        /// Python: <c>str.capitalize()</c>
        /// </summary>
        public static string Capitalize(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            if (s.Length == 1)
            {
                return s.ToUpperInvariant();
            }

            return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
        }

        /// <summary>
        /// Return the lowest index in the string where substring <paramref name="sub"/>
        /// is found. Return -1 if <paramref name="sub"/> is not found.
        /// Python: <c>str.find(sub)</c>
        /// </summary>
        public static int Find(this string s, string sub)
        {
            return s.IndexOf(sub, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return the lowest index in the string where substring <paramref name="sub"/>
        /// is found, starting the search at position <paramref name="start"/>.
        /// Return -1 if <paramref name="sub"/> is not found.
        /// Python: <c>str.find(sub, start)</c>
        /// </summary>
        public static int Find(this string s, string sub, int start)
        {
            if (start < 0)
            {
                start = System.Math.Max(0, s.Length + start);
            }

            if (start > s.Length)
            {
                return -1;
            }

            if (start == s.Length)
            {
                return string.IsNullOrEmpty(sub) ? s.Length : -1;
            }

            return s.IndexOf(sub, start, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return the lowest index in the string where substring <paramref name="sub"/>
        /// is found within <c>s[start:end]</c>.
        /// Return -1 if <paramref name="sub"/> is not found.
        /// Python: <c>str.find(sub, start, end)</c>
        /// </summary>
        public static int Find(this string s, string sub, int start, int end)
        {
            if (start < 0)
                start = System.Math.Max(0, s.Length + start);
            if (end < 0)
                end = System.Math.Max(0, s.Length + end);
            if (end > s.Length)
                end = s.Length;
            if (start > end || start > s.Length)
                return -1;
            if (start == end)
                return string.IsNullOrEmpty(sub) ? start : -1;

            int count = end - start;
            int index = s.IndexOf(sub, start, count, StringComparison.Ordinal);
            return index;
        }

        /// <summary>
        /// Return the highest index in the string where substring <paramref name="sub"/>
        /// is found. Return -1 if <paramref name="sub"/> is not found.
        /// Python: <c>str.rfind(sub)</c>
        /// </summary>
        public static int Rfind(this string s, string sub)
        {
            return s.LastIndexOf(sub, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return the highest index in the string where substring <paramref name="sub"/>
        /// is found, searching within <c>s[start:]</c>.
        /// Return -1 if <paramref name="sub"/> is not found.
        /// Python: <c>str.rfind(sub, start)</c>
        /// </summary>
        public static int Rfind(this string s, string sub, int start)
        {
            if (start < 0)
            {
                start = System.Math.Max(0, s.Length + start);
            }

            if (start > s.Length)
            {
                return -1;
            }

            if (start == s.Length)
            {
                return string.IsNullOrEmpty(sub) ? s.Length : -1;
            }

            // Python rfind(sub, start) searches in s[start:]
            var substring = s.Substring(start);
            var index = substring.LastIndexOf(sub, StringComparison.Ordinal);
            return index >= 0 ? start + index : -1;
        }

        /// <summary>
        /// Return the highest index in the string where substring <paramref name="sub"/>
        /// is found within <c>s[start:end]</c>.
        /// Return -1 if <paramref name="sub"/> is not found.
        /// Python: <c>str.rfind(sub, start, end)</c>
        /// </summary>
        public static int Rfind(this string s, string sub, int start, int end)
        {
            if (start < 0)
                start = System.Math.Max(0, s.Length + start);
            if (end < 0)
                end = System.Math.Max(0, s.Length + end);
            if (end > s.Length)
                end = s.Length;
            if (start > end || start > s.Length)
                return -1;
            if (start == end)
                return string.IsNullOrEmpty(sub) ? start : -1;

            var slice = s.Substring(start, end - start);
            int index = slice.LastIndexOf(sub, StringComparison.Ordinal);
            return index >= 0 ? start + index : -1;
        }

        /// <summary>
        /// Return a string which is the concatenation of the strings in
        /// <paramref name="iterable"/>. The separator between elements is the
        /// string providing this method.
        /// Python: <c>str.join(iterable)</c>
        /// </summary>
        public static string Join(this string s, IEnumerable<string> iterable)
        {
            return string.Join(s, iterable);
        }

        // ----------------------------------------------------------------
        // Priority 2 — additional methods
        // ----------------------------------------------------------------

        /// <summary>
        /// Return a titlecased version of the string where words start with
        /// an upper case character and the remaining characters are lower case.
        /// Python: <c>str.title()</c>
        /// </summary>
        public static string Title(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var result = new StringBuilder(s.Length);
            bool previousWasLetter = false;

            foreach (char c in s)
            {
                if (char.IsLetter(c))
                {
                    if (!previousWasLetter)
                    {
                        result.Append(char.ToUpperInvariant(c));
                    }
                    else
                    {
                        result.Append(char.ToLowerInvariant(c));
                    }
                    previousWasLetter = true;
                }
                else
                {
                    result.Append(c);
                    previousWasLetter = false;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Return a copy of the string with uppercase characters converted to
        /// lowercase and vice versa.
        /// Python: <c>str.swapcase()</c>
        /// </summary>
        public static string Swapcase(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var result = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsUpper(c))
                {
                    result.Append(char.ToLowerInvariant(c));
                }
                else if (char.IsLower(c))
                {
                    result.Append(char.ToUpperInvariant(c));
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Return a casefolded copy of the string. Casefolded strings may be
        /// used for caseless matching.
        /// Python: <c>str.casefold()</c>
        /// </summary>
        /// <remarks>
        /// Performs full Unicode case folding matching Python behavior
        /// (e.g., ß → ss, ﬁ → fi).
        /// </remarks>
        public static string Casefold(this string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                sb.Append(CaseFoldChar(c));
            }
            return sb.ToString();
        }

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

        /// <summary>
        /// Return <c>true</c> if all characters in the string are digits and
        /// there is at least one character, <c>false</c> otherwise.
        /// Python: <c>str.isdigit()</c>
        /// </summary>
        public static bool Isdigit(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return <c>true</c> if all characters in the string are alphabetic
        /// and there is at least one character, <c>false</c> otherwise.
        /// Python: <c>str.isalpha()</c>
        /// </summary>
        public static bool Isalpha(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!char.IsLetter(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return <c>true</c> if all characters in the string are alphanumeric
        /// and there is at least one character, <c>false</c> otherwise.
        /// Python: <c>str.isalnum()</c>
        /// </summary>
        public static bool Isalnum(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return <c>true</c> if all characters in the string are whitespace
        /// and there is at least one character, <c>false</c> otherwise.
        /// Python: <c>str.isspace()</c>
        /// </summary>
        public static bool Isspace(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!char.IsWhiteSpace(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return <c>true</c> if all cased characters in the string are
        /// uppercase and there is at least one cased character, <c>false</c>
        /// otherwise.
        /// Python: <c>str.isupper()</c>
        /// </summary>
        public static bool Isupper(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            bool hasCased = false;
            foreach (char c in s)
            {
                if (char.IsLower(c))
                {
                    return false;
                }
                if (char.IsUpper(c))
                {
                    hasCased = true;
                }
            }

            return hasCased;
        }

        /// <summary>
        /// Return <c>true</c> if all cased characters in the string are
        /// lowercase and there is at least one cased character, <c>false</c>
        /// otherwise.
        /// Python: <c>str.islower()</c>
        /// </summary>
        public static bool Islower(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            bool hasCased = false;
            foreach (char c in s)
            {
                if (char.IsUpper(c))
                {
                    return false;
                }
                if (char.IsLower(c))
                {
                    hasCased = true;
                }
            }

            return hasCased;
        }

        /// <summary>
        /// Return the number of non-overlapping occurrences of substring
        /// <paramref name="sub"/> in the string.
        /// Python: <c>str.count(sub)</c>
        /// </summary>
        public static int Count(this string s, string sub)
        {
            if (string.IsNullOrEmpty(sub))
            {
                // Python behavior: empty string occurs before, between, and after every character
                return s.Length + 1;
            }

            int count = 0;
            int index = 0;
            while ((index = s.IndexOf(sub, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += sub.Length;
            }

            return count;
        }

        /// <summary>
        /// Return centered in a string of length <paramref name="width"/>.
        /// Padding is done using the specified <paramref name="fillchar"/>
        /// (default is a space).
        /// Python: <c>str.center(width, fillchar)</c>
        /// </summary>
        public static string Center(this string s, int width, char fillchar = ' ')
        {
            if (s.Length >= width)
            {
                return s;
            }

            int totalPadding = width - s.Length;
            int leftPadding = totalPadding / 2;
            int rightPadding = totalPadding - leftPadding;

            return new string(fillchar, leftPadding) + s + new string(fillchar, rightPadding);
        }

        /// <summary>
        /// Return the string left-justified in a string of length
        /// <paramref name="width"/>. Padding is done using the specified
        /// <paramref name="fillchar"/> (default is a space).
        /// Python: <c>str.ljust(width, fillchar)</c>
        /// </summary>
        public static string Ljust(this string s, int width, char fillchar = ' ')
        {
            if (s.Length >= width)
            {
                return s;
            }

            return s.PadRight(width, fillchar);
        }

        /// <summary>
        /// Return the string right-justified in a string of length
        /// <paramref name="width"/>. Padding is done using the specified
        /// <paramref name="fillchar"/> (default is a space).
        /// Python: <c>str.rjust(width, fillchar)</c>
        /// </summary>
        public static string Rjust(this string s, int width, char fillchar = ' ')
        {
            if (s.Length >= width)
            {
                return s;
            }

            return s.PadLeft(width, fillchar);
        }

        /// <summary>
        /// Return a copy of the string left filled with ASCII '0' digits to
        /// make a string of length <paramref name="width"/>. A leading sign
        /// prefix (+/-) is handled by inserting the padding after the sign
        /// character rather than before.
        /// Python: <c>str.zfill(width)</c>
        /// </summary>
        public static string Zfill(this string s, int width)
        {
            if (s.Length >= width)
            {
                return s;
            }

            int fillCount = width - s.Length;

            // Handle leading sign
            if (s.Length > 0 && (s[0] == '+' || s[0] == '-'))
            {
                return s[0] + new string('0', fillCount) + s.Substring(1);
            }

            return new string('0', fillCount) + s;
        }

        /// <summary>
        /// Return a list of the lines in the string, breaking at line
        /// boundaries. Line breaks are not included in the resulting list.
        /// Python: <c>str.splitlines()</c>
        /// </summary>
        /// <remarks>
        /// Recognizes all Python line boundaries: \n, \r\n, \r, \v (0x0B),
        /// \f (0x0C), \x1C, \x1D, \x1E, \x85 (NEL), \u2028 (LS), \u2029 (PS).
        /// </remarks>
        public static List<string> Splitlines(this string s)
        {
            return Splitlines(s, false);
        }

        /// <summary>
        /// Return a list of the lines in the string, breaking at line
        /// boundaries. When <paramref name="keepends"/> is <c>true</c>, line
        /// break characters are included in the resulting strings.
        /// Python: <c>str.splitlines(keepends)</c>
        /// </summary>
        /// <remarks>
        /// Recognizes all Python line boundaries: \n, \r\n, \r, \v (0x0B),
        /// \f (0x0C), \x1C, \x1D, \x1E, \x85 (NEL), \u2028 (LS), \u2029 (PS).
        /// </remarks>
        public static List<string> Splitlines(this string s, bool keepends)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(s))
            {
                return result;
            }

            var currentLine = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (c == '\r')
                {
                    // Handle \r\n and \r
                    if (i + 1 < s.Length && s[i + 1] == '\n')
                    {
                        if (keepends)
                        {
                            currentLine.Append("\r\n");
                        }
                        i++; // Skip the \n
                    }
                    else
                    {
                        if (keepends)
                        {
                            currentLine.Append(c);
                        }
                    }
                    result.Add(currentLine.ToString());
                    currentLine.Clear();
                }
                else if (c == '\n' || c == '\x0B' || c == '\x0C'
                    || c == '\x1C' || c == '\x1D' || c == '\x1E'
                    || c == '\x85' || c == '\u2028' || c == '\u2029')
                {
                    if (keepends)
                    {
                        currentLine.Append(c);
                    }
                    result.Add(currentLine.ToString());
                    currentLine.Clear();
                }
                else
                {
                    currentLine.Append(c);
                }
            }

            // Add remaining content if any
            if (currentLine.Length > 0)
            {
                result.Add(currentLine.ToString());
            }

            return result;
        }

        /// <summary>
        /// If the string starts with the <paramref name="prefix"/> string,
        /// return <c>string[len(prefix):]</c>. Otherwise, return a copy of
        /// the original string.
        /// Python: <c>str.removeprefix(prefix)</c>
        /// </summary>
        public static string Removeprefix(this string s, string prefix)
        {
            if (s.StartsWith(prefix, StringComparison.Ordinal))
            {
                return s.Substring(prefix.Length);
            }

            return s;
        }

        /// <summary>
        /// If the string ends with the <paramref name="suffix"/> string,
        /// return <c>string[:-len(suffix)]</c>. Otherwise, return a copy of
        /// the original string.
        /// Python: <c>str.removesuffix(suffix)</c>
        /// </summary>
        public static string Removesuffix(this string s, string suffix)
        {
            if (s.EndsWith(suffix, StringComparison.Ordinal))
            {
                return s.Substring(0, s.Length - suffix.Length);
            }

            return s;
        }

        // ----------------------------------------------------------------
        // Priority 3 — split, replace, starts/endswith, index, partition,
        //               expandtabs, istitle, encode, maketrans, translate
        // ----------------------------------------------------------------

        /// <summary>
        /// Return a list of the words in the string, using whitespace as the
        /// delimiter. Consecutive whitespace is collapsed and leading/trailing
        /// whitespace is stripped.
        /// Python: <c>str.split()</c>
        /// </summary>
        public static List<string> Split(this string s)
        {
            var result = new List<string>();
            int i = 0;
            int len = s.Length;

            while (i < len)
            {
                // Skip whitespace
                while (i < len && char.IsWhiteSpace(s[i]))
                {
                    i++;
                }

                if (i >= len)
                {
                    break;
                }

                // Find end of word
                int start = i;
                while (i < len && !char.IsWhiteSpace(s[i]))
                {
                    i++;
                }

                result.Append(s.Substring(start, i - start));
            }

            return result;
        }

        /// <summary>
        /// Return a list of the words in the string, using <paramref name="sep"/>
        /// as the delimiter string.
        /// Python: <c>str.split(sep)</c>
        /// </summary>
        public static List<string> Split(this string s, string sep)
        {
            if (string.IsNullOrEmpty(sep))
            {
                throw new ValueError("empty separator");
            }

            var result = new List<string>();
            int start = 0;
            int index;

            while ((index = s.IndexOf(sep, start, StringComparison.Ordinal)) >= 0)
            {
                result.Append(s.Substring(start, index - start));
                start = index + sep.Length;
            }

            result.Append(s.Substring(start));
            return result;
        }

        /// <summary>
        /// Return a list of the words in the string, using <paramref name="sep"/>
        /// as the delimiter string, splitting at most <paramref name="maxsplit"/>
        /// times from the left.
        /// Python: <c>str.split(sep, maxsplit)</c>
        /// </summary>
        public static List<string> Split(this string s, string sep, int maxsplit)
        {
            if (string.IsNullOrEmpty(sep))
            {
                throw new ValueError("empty separator");
            }

            if (maxsplit < 0)
            {
                return Split(s, sep);
            }

            if (maxsplit == 0)
            {
                var single = new List<string>();
                single.Append(s);
                return single;
            }

            var result = new List<string>();
            int start = 0;
            int splits = 0;
            int index;

            while (splits < maxsplit && (index = s.IndexOf(sep, start, StringComparison.Ordinal)) >= 0)
            {
                result.Append(s.Substring(start, index - start));
                start = index + sep.Length;
                splits++;
            }

            result.Append(s.Substring(start));
            return result;
        }

        /// <summary>
        /// Return a list of the words in the string, using whitespace as the
        /// delimiter, splitting from the right. Consecutive whitespace is
        /// collapsed.
        /// Python: <c>str.rsplit()</c>
        /// </summary>
        public static List<string> Rsplit(this string s)
        {
            // rsplit() with no args behaves the same as split() with no args
            return Split(s);
        }

        /// <summary>
        /// Return a list of the words in the string, using <paramref name="sep"/>
        /// as the delimiter string, splitting from the right.
        /// Python: <c>str.rsplit(sep)</c>
        /// </summary>
        public static List<string> Rsplit(this string s, string sep)
        {
            // With no maxsplit, rsplit == split
            return Split(s, sep);
        }

        /// <summary>
        /// Return a list of the words in the string, using <paramref name="sep"/>
        /// as the delimiter string, splitting at most <paramref name="maxsplit"/>
        /// times from the right.
        /// Python: <c>str.rsplit(sep, maxsplit)</c>
        /// </summary>
        public static List<string> Rsplit(this string s, string sep, int maxsplit)
        {
            if (string.IsNullOrEmpty(sep))
            {
                throw new ValueError("empty separator");
            }

            if (maxsplit < 0)
            {
                return Split(s, sep);
            }

            if (maxsplit == 0)
            {
                var single = new List<string>();
                single.Append(s);
                return single;
            }

            // Split from right: collect parts in reverse, then reverse
            var parts = new System.Collections.Generic.List<string>();
            int end = s.Length;
            int splits = 0;
            int index;

            while (splits < maxsplit && (index = s.LastIndexOf(sep, end - 1, StringComparison.Ordinal)) >= 0)
            {
                parts.Add(s.Substring(index + sep.Length, end - index - sep.Length));
                end = index;
                splits++;
            }

            parts.Add(s.Substring(0, end));
            parts.Reverse();

            var result = new List<string>();
            foreach (var part in parts)
            {
                result.Append(part);
            }

            return result;
        }

        /// <summary>
        /// Return a copy with all occurrences of substring <paramref name="old"/>
        /// replaced by <paramref name="new"/>.
        /// Python: <c>str.replace(old, new)</c>
        /// </summary>
        public static string Replace(this string s, string old, string @new)
        {
            if (string.IsNullOrEmpty(old))
            {
                // Python behavior: insert new between every character
                var sb = new StringBuilder((s.Length + 1) * @new.Length + s.Length);
                sb.Append(@new);
                foreach (char c in s)
                {
                    sb.Append(c);
                    sb.Append(@new);
                }
                return sb.ToString();
            }

            return s.Replace(old, @new);
        }

        /// <summary>
        /// Return a copy with the first <paramref name="count"/> occurrences of
        /// substring <paramref name="old"/> replaced by <paramref name="new"/>.
        /// Python: <c>str.replace(old, new, count)</c>
        /// </summary>
        public static string Replace(this string s, string old, string @new, int count)
        {
            if (count < 0)
            {
                return Replace(s, old, @new);
            }

            if (count == 0)
            {
                return s;
            }

            if (string.IsNullOrEmpty(old))
            {
                // Python: insert new before each char, up to count times
                var sb = new StringBuilder();
                int replacements = 0;

                if (replacements < count)
                {
                    sb.Append(@new);
                    replacements++;
                }

                foreach (char c in s)
                {
                    sb.Append(c);
                    if (replacements < count)
                    {
                        sb.Append(@new);
                        replacements++;
                    }
                }

                return sb.ToString();
            }

            var result = new StringBuilder(s.Length);
            int start = 0;
            int replacementsDone = 0;
            int index;

            while (replacementsDone < count && (index = s.IndexOf(old, start, StringComparison.Ordinal)) >= 0)
            {
                result.Append(s, start, index - start);
                result.Append(@new);
                start = index + old.Length;
                replacementsDone++;
            }

            result.Append(s, start, s.Length - start);
            return result.ToString();
        }

        /// <summary>
        /// Return <c>true</c> if the string starts with the <paramref name="prefix"/>.
        /// Python: <c>str.startswith(prefix)</c>
        /// </summary>
        public static bool Startswith(this string s, string prefix)
        {
            return s.StartsWith(prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return <c>true</c> if the string starts with the <paramref name="prefix"/>,
        /// beginning the check at position <paramref name="start"/>.
        /// Python: <c>str.startswith(prefix, start)</c>
        /// </summary>
        public static bool Startswith(this string s, string prefix, int start)
        {
            if (start < 0)
            {
                start = System.Math.Max(0, s.Length + start);
            }

            if (start > s.Length)
            {
                return false;
            }

            return s.Substring(start).StartsWith(prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return <c>true</c> if the string starts with the <paramref name="prefix"/>,
        /// considering only <c>s[start:end]</c>.
        /// Python: <c>str.startswith(prefix, start, end)</c>
        /// </summary>
        public static bool Startswith(this string s, string prefix, int start, int end)
        {
            if (start < 0)
                start = System.Math.Max(0, s.Length + start);
            if (end < 0)
                end = System.Math.Max(0, s.Length + end);
            if (end > s.Length)
                end = s.Length;
            if (start > end)
                return false;

            var slice = s.Substring(start, end - start);
            return slice.StartsWith(prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return <c>true</c> if the string starts with any of the given
        /// <paramref name="prefixes"/>.
        /// Python: <c>str.startswith(tuple_of_prefixes)</c>
        /// </summary>
        public static bool Startswith(this string s, params string[] prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (s.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Return <c>true</c> if the string ends with the <paramref name="suffix"/>.
        /// Python: <c>str.endswith(suffix)</c>
        /// </summary>
        public static bool Endswith(this string s, string suffix)
        {
            return s.EndsWith(suffix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return <c>true</c> if the string ends with the <paramref name="suffix"/>,
        /// considering only <c>s[:end]</c> or <c>s[start:]</c>.
        /// Python: <c>str.endswith(suffix, start)</c>
        /// </summary>
        public static bool Endswith(this string s, string suffix, int start)
        {
            if (start < 0)
            {
                start = System.Math.Max(0, s.Length + start);
            }

            if (start > s.Length)
            {
                return false;
            }

            return s.Substring(start).EndsWith(suffix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return <c>true</c> if the string ends with the <paramref name="suffix"/>,
        /// considering only <c>s[start:end]</c>.
        /// Python: <c>str.endswith(suffix, start, end)</c>
        /// </summary>
        public static bool Endswith(this string s, string suffix, int start, int end)
        {
            if (start < 0)
                start = System.Math.Max(0, s.Length + start);
            if (end < 0)
                end = System.Math.Max(0, s.Length + end);
            if (end > s.Length)
                end = s.Length;
            if (start > end)
                return false;

            var slice = s.Substring(start, end - start);
            return slice.EndsWith(suffix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return <c>true</c> if the string ends with any of the given
        /// <paramref name="suffixes"/>.
        /// Python: <c>str.endswith(tuple_of_suffixes)</c>
        /// </summary>
        public static bool Endswith(this string s, params string[] suffixes)
        {
            foreach (var suffix in suffixes)
            {
                if (s.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Like <see cref="Find(string, string)"/> but raises <see cref="ValueError"/>
        /// when the substring is not found.
        /// Python: <c>str.index(sub)</c>
        /// </summary>
        public static int Index(this string s, string sub)
        {
            int result = s.Find(sub);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }

            return result;
        }

        /// <summary>
        /// Like <see cref="Find(string, string, int)"/> but raises
        /// <see cref="ValueError"/> when the substring is not found.
        /// Python: <c>str.index(sub, start)</c>
        /// </summary>
        public static int Index(this string s, string sub, int start)
        {
            int result = s.Find(sub, start);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }

            return result;
        }

        /// <summary>
        /// Like <see cref="Find(string, string, int, int)"/> but raises
        /// <see cref="ValueError"/> when the substring is not found.
        /// Python: <c>str.index(sub, start, end)</c>
        /// </summary>
        public static int Index(this string s, string sub, int start, int end)
        {
            int result = s.Find(sub, start, end);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }

            return result;
        }

        /// <summary>
        /// Like <see cref="Rfind(string, string)"/> but raises
        /// <see cref="ValueError"/> when the substring is not found.
        /// Python: <c>str.rindex(sub)</c>
        /// </summary>
        public static int Rindex(this string s, string sub)
        {
            int result = s.Rfind(sub);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }

            return result;
        }

        /// <summary>
        /// Like <see cref="Rfind(string, string, int)"/> but raises
        /// <see cref="ValueError"/> when the substring is not found.
        /// Python: <c>str.rindex(sub, start)</c>
        /// </summary>
        public static int Rindex(this string s, string sub, int start)
        {
            int result = s.Rfind(sub, start);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }

            return result;
        }

        /// <summary>
        /// Like <see cref="Rfind(string, string, int, int)"/> but raises
        /// <see cref="ValueError"/> when the substring is not found.
        /// Python: <c>str.rindex(sub, start, end)</c>
        /// </summary>
        public static int Rindex(this string s, string sub, int start, int end)
        {
            int result = s.Rfind(sub, start, end);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }

            return result;
        }

        /// <summary>
        /// Split the string at the first occurrence of <paramref name="sep"/>,
        /// and return a 3-tuple containing the part before the separator, the
        /// separator itself, and the part after the separator.
        /// Python: <c>str.partition(sep)</c>
        /// </summary>
        public static (string, string, string) Partition(this string s, string sep)
        {
            if (string.IsNullOrEmpty(sep))
            {
                throw new ValueError("empty separator");
            }

            int index = s.IndexOf(sep, StringComparison.Ordinal);
            if (index < 0)
            {
                return (s, "", "");
            }

            return (s.Substring(0, index), sep, s.Substring(index + sep.Length));
        }

        /// <summary>
        /// Split the string at the last occurrence of <paramref name="sep"/>,
        /// and return a 3-tuple containing the part before the separator, the
        /// separator itself, and the part after the separator.
        /// Python: <c>str.rpartition(sep)</c>
        /// </summary>
        public static (string, string, string) Rpartition(this string s, string sep)
        {
            if (string.IsNullOrEmpty(sep))
            {
                throw new ValueError("empty separator");
            }

            int index = s.LastIndexOf(sep, StringComparison.Ordinal);
            if (index < 0)
            {
                return ("", "", s);
            }

            return (s.Substring(0, index), sep, s.Substring(index + sep.Length));
        }

        /// <summary>
        /// Return a copy where all tab characters are replaced by one or more
        /// spaces, depending on the current column and the given tab size.
        /// Python: <c>str.expandtabs(tabsize=8)</c>
        /// </summary>
        public static string Expandtabs(this string s, int tabsize = 8)
        {
            if (s.Length == 0)
            {
                return s;
            }

            var sb = new StringBuilder();
            int column = 0;

            foreach (char c in s)
            {
                if (c == '\t')
                {
                    if (tabsize <= 0)
                    {
                        // tabsize <= 0: tab replaced by nothing, column stays
                    }
                    else
                    {
                        int spaces = tabsize - (column % tabsize);
                        sb.Append(' ', spaces);
                        column += spaces;
                    }
                }
                else if (c == '\n' || c == '\r')
                {
                    sb.Append(c);
                    column = 0;
                }
                else
                {
                    sb.Append(c);
                    column++;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Return <c>true</c> if the string is a titlecased string and there
        /// is at least one character: uppercase characters may only follow
        /// uncased characters and lowercase characters only cased characters.
        /// Python: <c>str.istitle()</c>
        /// </summary>
        public static bool Istitle(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            bool hasCased = false;
            bool previousWasCased = false;

            foreach (char c in s)
            {
                if (char.IsUpper(c))
                {
                    if (previousWasCased)
                    {
                        return false;
                    }
                    hasCased = true;
                    previousWasCased = true;
                }
                else if (char.IsLower(c))
                {
                    if (!previousWasCased)
                    {
                        return false;
                    }
                    hasCased = true;
                    previousWasCased = true;
                }
                else
                {
                    previousWasCased = false;
                }
            }

            return hasCased;
        }

        /// <summary>
        /// Return the string encoded to bytes using the specified encoding.
        /// Python: <c>str.encode(encoding='utf-8')</c>
        /// </summary>
        public static byte[] Encode(this string s, string encoding = "utf-8")
        {
            switch (encoding.ToLowerInvariant().Replace("-", ""))
            {
                case "utf8":
                    return Encoding.UTF8.GetBytes(s);
                case "ascii":
                    return Encoding.ASCII.GetBytes(s);
                case "utf16":
                case "utf16le":
                    return Encoding.Unicode.GetBytes(s);
                case "utf16be":
                    return Encoding.BigEndianUnicode.GetBytes(s);
                case "latin1":
                case "iso88591":
                    return Encoding.GetEncoding("iso-8859-1").GetBytes(s);
                default:
                    throw new ValueError($"unknown encoding: {encoding}");
            }
        }

        /// <summary>
        /// Return a translation table usable for <see cref="Translate"/>.
        /// Python: <c>str.maketrans(x, y)</c>
        /// </summary>
        public static Dict<char, string?> Maketrans(string x, string y)
        {
            if (x.Length != y.Length)
            {
                throw new ValueError("the first two maketrans arguments must have equal length");
            }

            var table = new Dict<char, string?>();
            for (int i = 0; i < x.Length; i++)
            {
                table[x[i]] = y[i].ToString();
            }

            return table;
        }

        /// <summary>
        /// Return a translation table usable for <see cref="Translate"/>.
        /// Characters in <paramref name="z"/> are mapped to deletion (null).
        /// Python: <c>str.maketrans(x, y, z)</c>
        /// </summary>
        public static Dict<char, string?> Maketrans(string x, string y, string z)
        {
            if (x.Length != y.Length)
            {
                throw new ValueError("the first two maketrans arguments must have equal length");
            }

            var table = new Dict<char, string?>();
            for (int i = 0; i < x.Length; i++)
            {
                table[x[i]] = y[i].ToString();
            }

            foreach (char c in z)
            {
                table[c] = null;
            }

            return table;
        }

        /// <summary>
        /// Return a copy of the string in which each character has been mapped
        /// through the given translation table. Characters mapped to
        /// <c>null</c> are deleted.
        /// Python: <c>str.translate(table)</c>
        /// </summary>
        public static string Translate(this string s, Dict<char, string?> table)
        {
            var sb = new StringBuilder(s.Length);

            foreach (char c in s)
            {
                if (table.ContainsKey(c))
                {
                    var mapped = table[c];
                    if (mapped != null)
                    {
                        sb.Append(mapped);
                    }
                    // null means delete
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
