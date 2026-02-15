using System;
using System.Collections.Generic;
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
        /// (e.g., ß → ss, ﬁ → fi). Delegates to <see cref="Str.CaseFold"/>.
        /// </remarks>
        public static string Casefold(this string s)
        {
            return new Str(s).CaseFold().ToString();
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
    }
}
