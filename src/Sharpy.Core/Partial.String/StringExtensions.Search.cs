using System;

namespace Sharpy
{
    /// <summary>
    /// Search and query methods for StringExtensions: Find, Rfind, Index, Rindex,
    /// Count, Startswith, Endswith, and character-class checks.
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// Return the lowest index in the string where substring <paramref name="sub"/>
        /// is found. Return -1 if <paramref name="sub"/> is not found.
        /// Python: <c>str.find(sub)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "hello".find("ll")    # 2
        /// "hello".find("xy")    # -1
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "hello hello".rfind("hello")    # 6
        /// </code>
        /// </example>
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
        /// Return <c>true</c> if all characters in the string are digits and
        /// there is at least one character, <c>false</c> otherwise.
        /// Python: <c>str.isdigit()</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "123".isdigit()     # True
        /// "12.3".isdigit()    # False
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "hello".isalpha()     # True
        /// "hello1".isalpha()    # False
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "abc123".isalnum()    # True
        /// "abc 123".isalnum()   # False
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "banana".count("an")    # 2
        /// "hello".count("x")      # 0
        /// </code>
        /// </example>
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

        // ----------------------------------------------------------------
        // Startswith / Endswith
        // ----------------------------------------------------------------

        /// <summary>
        /// Return <c>true</c> if string starts with the <paramref name="prefix"/>.
        /// Python: <c>str.startswith(prefix)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "hello".startswith("he")    # True
        /// "hello".startswith("lo")    # False
        /// </code>
        /// </example>
        public static bool Startswith(this string s, string prefix)
        {
            return s.StartsWith(prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return <c>true</c> if <c>s[start:]</c> starts with the <paramref name="prefix"/>.
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
        /// Return <c>true</c> if <c>s[start:end]</c> starts with the <paramref name="prefix"/>.
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
        /// Return <c>true</c> if string ends with the <paramref name="suffix"/>.
        /// Python: <c>str.endswith(suffix)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "hello".endswith("lo")    # True
        /// "hello".endswith("he")    # False
        /// </code>
        /// </example>
        public static bool Endswith(this string s, string suffix)
        {
            return s.EndsWith(suffix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return <c>true</c> if <c>s[start:]</c> ends with the <paramref name="suffix"/>.
        /// Python: <c>str.endswith(suffix, start)</c>
        /// </summary>
        public static bool Endswith(this string s, string suffix, int start)
        {
            return Endswith(s, suffix, start, s.Length);
        }

        /// <summary>
        /// Return <c>true</c> if <c>s[start:end]</c> ends with the <paramref name="suffix"/>.
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
        // Index / Rindex
        // ----------------------------------------------------------------

        /// <summary>
        /// Like <see cref="Find(string, string)"/> but raises <see cref="ValueError"/>
        /// when the substring is not found.
        /// Python: <c>str.index(sub)</c>
        /// </summary>
        /// <exception cref="ValueError">Thrown if the substring is not found.</exception>
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
        /// Like <see cref="Find(string, string, int)"/> but raises <see cref="ValueError"/>
        /// when the substring is not found.
        /// Python: <c>str.index(sub, start)</c>
        /// </summary>
        /// <exception cref="ValueError">Thrown if the substring is not found.</exception>
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
        /// Like <see cref="Find(string, string, int, int)"/> but raises <see cref="ValueError"/>
        /// when the substring is not found.
        /// Python: <c>str.index(sub, start, end)</c>
        /// </summary>
        /// <exception cref="ValueError">Thrown if the substring is not found.</exception>
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
        /// Like <see cref="Rfind(string, string)"/> but raises <see cref="ValueError"/>
        /// when the substring is not found.
        /// Python: <c>str.rindex(sub)</c>
        /// </summary>
        /// <exception cref="ValueError">Thrown if the substring is not found.</exception>
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
        /// Like <see cref="Rfind(string, string, int)"/> but raises <see cref="ValueError"/>
        /// when the substring is not found.
        /// Python: <c>str.rindex(sub, start)</c>
        /// </summary>
        /// <exception cref="ValueError">Thrown if the substring is not found.</exception>
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
        /// Like <see cref="Rfind(string, string, int, int)"/> but raises <see cref="ValueError"/>
        /// when the substring is not found.
        /// Python: <c>str.rindex(sub, start, end)</c>
        /// </summary>
        /// <exception cref="ValueError">Thrown if the substring is not found.</exception>
        public static int Rindex(this string s, string sub, int start, int end)
        {
            int result = Rfind(s, sub, start, end);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }
    }
}
