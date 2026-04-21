using System;

namespace Sharpy
{
    /// <summary>
    /// Search and predicate extension methods for string.
    /// </summary>
    public static partial class StringExtensions
    {
        // ----------------------------------------------------------------
        // Find / Rfind
        // ----------------------------------------------------------------

        /// <summary>
        /// Return the lowest index where substring <paramref name="sub"/> is found.
        /// Return -1 if not found.
        /// Python: <c>str.find(sub)</c>
        /// </summary>
        public static int Find(this string s, string sub)
        {
            return s.IndexOf(sub, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return the lowest index where substring <paramref name="sub"/> is found,
        /// starting the search at <paramref name="start"/>.
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
        /// Return the lowest index where substring <paramref name="sub"/> is found
        /// within <c>s[start:end]</c>.
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
            return s.IndexOf(sub, start, count, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return the highest index where substring <paramref name="sub"/> is found.
        /// Return -1 if not found.
        /// Python: <c>str.rfind(sub)</c>
        /// </summary>
        public static int Rfind(this string s, string sub)
        {
            return s.LastIndexOf(sub, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return the highest index where substring <paramref name="sub"/> is found,
        /// searching within <c>s[start:]</c>.
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

            var substring = s.Substring(start);
            var index = substring.LastIndexOf(sub, StringComparison.Ordinal);
            return index >= 0 ? start + index : -1;
        }

        /// <summary>
        /// Return the highest index where substring <paramref name="sub"/> is found
        /// within <c>s[start:end]</c>.
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

        // ----------------------------------------------------------------
        // Index / Rindex
        // ----------------------------------------------------------------

        /// <summary>
        /// Like <see cref="Find(string, string)"/> but raises <see cref="ValueError"/>
        /// when the substring is not found.
        /// Python: <c>str.index(sub)</c>
        /// </summary>
        public static int Index(this string s, string sub)
        {
            int result = Find(s, sub);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Find(string, string, int)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.index(sub, start)</c>
        /// </summary>
        public static int Index(this string s, string sub, int start)
        {
            int result = Find(s, sub, start);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Find(string, string, int, int)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.index(sub, start, end)</c>
        /// </summary>
        public static int Index(this string s, string sub, int start, int end)
        {
            int result = Find(s, sub, start, end);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Rfind(string, string)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.rindex(sub)</c>
        /// </summary>
        public static int Rindex(this string s, string sub)
        {
            int result = Rfind(s, sub);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Rfind(string, string, int)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.rindex(sub, start)</c>
        /// </summary>
        public static int Rindex(this string s, string sub, int start)
        {
            int result = Rfind(s, sub, start);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Rfind(string, string, int, int)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.rindex(sub, start, end)</c>
        /// </summary>
        public static int Rindex(this string s, string sub, int start, int end)
        {
            int result = Rfind(s, sub, start, end);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        // ----------------------------------------------------------------
        // Count
        // ----------------------------------------------------------------

        /// <summary>
        /// Return the number of non-overlapping occurrences of substring
        /// <paramref name="sub"/>.
        /// Python: <c>str.count(sub)</c>
        /// </summary>
        public static int Count(this string s, string sub)
        {
            if (string.IsNullOrEmpty(sub))
            {
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

        // ----------------------------------------------------------------
        // Startswith / Endswith
        // ----------------------------------------------------------------

        /// <summary>
        /// Return true if the string starts with the <paramref name="prefix"/>.
        /// Python: <c>str.startswith(prefix)</c>
        /// </summary>
        public static bool Startswith(this string s, string prefix)
        {
            return s.StartsWith(prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return true if <c>s[start:]</c> starts with the <paramref name="prefix"/>.
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
            if (start + prefix.Length > s.Length)
            {
                return false;
            }
            return string.Compare(s, start, prefix, 0, prefix.Length, StringComparison.Ordinal) == 0;
        }

        /// <summary>
        /// Return true if <c>s[start:end]</c> starts with the <paramref name="prefix"/>.
        /// Python: <c>str.startswith(prefix, start, end)</c>
        /// </summary>
        public static bool Startswith(this string s, string prefix, int start, int end)
        {
            if (start < 0)
            {
                start = System.Math.Max(0, s.Length + start);
            }
            if (end < 0)
            {
                end = System.Math.Max(0, s.Length + end);
            }
            if (end > s.Length)
            {
                end = s.Length;
            }
            if (start > end)
            {
                return false;
            }
            int sliceLen = end - start;
            if (prefix.Length > sliceLen)
            {
                return false;
            }
            return string.Compare(s, start, prefix, 0, prefix.Length, StringComparison.Ordinal) == 0;
        }

        /// <summary>
        /// Return true if the string ends with the <paramref name="suffix"/>.
        /// Python: <c>str.endswith(suffix)</c>
        /// </summary>
        public static bool Endswith(this string s, string suffix)
        {
            return s.EndsWith(suffix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return true if <c>s[start:]</c> ends with the <paramref name="suffix"/>.
        /// Python: <c>str.endswith(suffix, start)</c>
        /// </summary>
        public static bool Endswith(this string s, string suffix, int start)
        {
            return Endswith(s, suffix, start, s.Length);
        }

        /// <summary>
        /// Return true if <c>s[start:end]</c> ends with the <paramref name="suffix"/>.
        /// Python: <c>str.endswith(suffix, start, end)</c>
        /// </summary>
        public static bool Endswith(this string s, string suffix, int start, int end)
        {
            if (start < 0)
            {
                start = System.Math.Max(0, s.Length + start);
            }
            if (end < 0)
            {
                end = System.Math.Max(0, s.Length + end);
            }
            if (end > s.Length)
            {
                end = s.Length;
            }
            if (start > end)
            {
                return false;
            }
            int sliceLen = end - start;
            if (suffix.Length > sliceLen)
            {
                return false;
            }
            int compareStart = end - suffix.Length;
            return string.Compare(s, compareStart, suffix, 0, suffix.Length, StringComparison.Ordinal) == 0;
        }

        // ----------------------------------------------------------------
        // Character-class predicates
        // ----------------------------------------------------------------

        /// <summary>
        /// Return true if all characters are digits and there is at least one character.
        /// Python: <c>str.isdigit()</c>
        /// </summary>
        public static bool Isdigit(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (char c in s)
            {
                if (!char.IsDigit(c) && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.OtherNumber)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Return true if all characters are alphabetic and there is at least one character.
        /// Python: <c>str.isalpha()</c>
        /// </summary>
        public static bool Isalpha(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (char c in s)
            {
                if (!char.IsLetter(c))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Return true if all characters are alphanumeric and there is at least one character.
        /// Python: <c>str.isalnum()</c>
        /// </summary>
        public static bool Isalnum(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (char c in s)
            {
                if (!char.IsLetterOrDigit(c))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Return true if all characters are whitespace and there is at least one character.
        /// Python: <c>str.isspace()</c>
        /// </summary>
        public static bool Isspace(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (char c in s)
            {
                if (!char.IsWhiteSpace(c))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Return true if all cased characters are uppercase and there is at least
        /// one cased character.
        /// Python: <c>str.isupper()</c>
        /// </summary>
        public static bool Isupper(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            bool hasCased = false;
            foreach (char c in s)
            {
                if (char.IsLower(c))
                    return false;
                if (char.IsUpper(c))
                    hasCased = true;
            }
            return hasCased;
        }

        /// <summary>
        /// Return true if all cased characters are lowercase and there is at least
        /// one cased character.
        /// Python: <c>str.islower()</c>
        /// </summary>
        public static bool Islower(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            bool hasCased = false;
            foreach (char c in s)
            {
                if (char.IsUpper(c))
                    return false;
                if (char.IsLower(c))
                    hasCased = true;
            }
            return hasCased;
        }

        /// <summary>
        /// Return true if the string is titlecased and there is at least one character.
        /// Python: <c>str.istitle()</c>
        /// </summary>
        public static bool Istitle(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            bool hasCased = false;
            bool previousWasCased = false;
            foreach (char c in s)
            {
                if (char.IsUpper(c))
                {
                    if (previousWasCased)
                        return false;
                    hasCased = true;
                    previousWasCased = true;
                }
                else if (char.IsLower(c))
                {
                    if (!previousWasCased)
                        return false;
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
        /// Return true if all characters are numeric and there is at least one character.
        /// Python: <c>str.isnumeric()</c>
        /// </summary>
        public static bool Isnumeric(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (char c in s)
            {
                if (!char.IsDigit(c) && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.OtherNumber)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Return true if all characters are decimal characters and there is at least one character.
        /// Python: <c>str.isdecimal()</c>
        /// </summary>
        public static bool Isdecimal(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (char c in s)
            {
                if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.DecimalDigitNumber)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Return true if the string is a valid Python identifier.
        /// Python: <c>str.isidentifier()</c>
        /// </summary>
        public static bool Isidentifier(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            char first = s[0];
            if (!char.IsLetter(first) && first != '_')
                return false;
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Return true if all characters are printable or the string is empty.
        /// Python: <c>str.isprintable()</c>
        /// </summary>
        public static bool Isprintable(this string s)
        {
            if (s.Length == 0)
                return true;
            foreach (char c in s)
            {
                var category = char.GetUnicodeCategory(c);
                if (category == System.Globalization.UnicodeCategory.Control
                    || category == System.Globalization.UnicodeCategory.Format
                    || category == System.Globalization.UnicodeCategory.Surrogate
                    || category == System.Globalization.UnicodeCategory.PrivateUse
                    || category == System.Globalization.UnicodeCategory.OtherNotAssigned
                    || category == System.Globalization.UnicodeCategory.LineSeparator
                    || category == System.Globalization.UnicodeCategory.ParagraphSeparator)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Return true if all characters are ASCII (U+0000 to U+007F) or the string is empty.
        /// Python: <c>str.isascii()</c>
        /// </summary>
        public static bool Isascii(this string s)
        {
            foreach (char c in s)
            {
                if (c > '\u007F')
                    return false;
            }
            return true;
        }
    }
}
