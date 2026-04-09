using System;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible string methods as extension methods on string.
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// Return a copy of the string converted to uppercase.
        /// Python: <c>str.upper()</c>
        /// </summary>
        public static string Upper(this string s) => s.ToUpperInvariant();

        /// <summary>
        /// Return a copy of the string converted to lowercase.
        /// Python: <c>str.lower()</c>
        /// </summary>
        public static string Lower(this string s) => s.ToLowerInvariant();

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
        /// Return a titlecased version of the string.
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
        /// Return a casefolded copy of the string.
        /// Python: <c>str.casefold()</c>
        /// </summary>
        public static string Casefold(this string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
            {
                sb.Append(CaseFoldChar(c));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Return a copy with leading and trailing whitespace removed.
        /// Python: <c>str.strip()</c>
        /// </summary>
        public static string Strip(this string s) => s.Trim();

        /// <summary>
        /// Return a copy with leading and trailing characters in
        /// <paramref name="chars"/> removed.
        /// Python: <c>str.strip(chars)</c>
        /// </summary>
        public static string Strip(this string s, string chars) => s.Trim(chars.ToCharArray());

        /// <summary>
        /// Return a copy with leading whitespace removed.
        /// Python: <c>str.lstrip()</c>
        /// </summary>
        public static string Lstrip(this string s) => s.TrimStart();

        /// <summary>
        /// Return a copy with leading characters in <paramref name="chars"/> removed.
        /// Python: <c>str.lstrip(chars)</c>
        /// </summary>
        public static string Lstrip(this string s, string chars) => s.TrimStart(chars.ToCharArray());

        /// <summary>
        /// Return a copy with trailing whitespace removed.
        /// Python: <c>str.rstrip()</c>
        /// </summary>
        public static string Rstrip(this string s) => s.TrimEnd();

        /// <summary>
        /// Return a copy with trailing characters in <paramref name="chars"/> removed.
        /// Python: <c>str.rstrip(chars)</c>
        /// </summary>
        public static string Rstrip(this string s, string chars) => s.TrimEnd(chars.ToCharArray());

        /// <summary>
        /// Return centered in a string of length <paramref name="width"/>.
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
        /// Return left-justified in a string of length <paramref name="width"/>.
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
        /// Return right-justified in a string of length <paramref name="width"/>.
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
        /// Return left filled with ASCII '0' digits to make a string of length
        /// <paramref name="width"/>. A leading sign prefix is handled.
        /// Python: <c>str.zfill(width)</c>
        /// </summary>
        public static string Zfill(this string s, int width)
        {
            if (s.Length >= width)
            {
                return s;
            }

            int fillCount = width - s.Length;

            if (s.Length > 0 && (s[0] == '+' || s[0] == '-'))
            {
                return s[0] + new string('0', fillCount) + s.Substring(1);
            }

            return new string('0', fillCount) + s;
        }

        /// <summary>
        /// If the string starts with the <paramref name="prefix"/>, return the
        /// string with the prefix removed. Otherwise, return a copy.
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
        /// If the string ends with the <paramref name="suffix"/>, return the
        /// string with the suffix removed. Otherwise, return a copy.
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

        /// <summary>
        /// Return a copy where all tab characters are expanded using spaces.
        /// Python: <c>str.expandtabs(tabsize=8)</c>
        /// </summary>
        public static string Expandtabs(this string s, int tabsize = 8)
        {
            var result = new StringBuilder();
            int column = 0;

            foreach (char c in s)
            {
                if (c == '\t')
                {
                    if (tabsize <= 0)
                    {
                        continue;
                    }
                    int spaces = tabsize - (column % tabsize);
                    result.Append(' ', spaces);
                    column += spaces;
                }
                else if (c == '\n' || c == '\r')
                {
                    result.Append(c);
                    column = 0;
                }
                else
                {
                    result.Append(c);
                    column++;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Return a copy with all occurrences of <paramref name="old"/> replaced
        /// by <paramref name="new_"/>.
        /// Python: <c>str.replace(old, new)</c>
        /// </summary>
        public static string Replace(this string s, string old, string new_)
        {
            if (old.Length == 0)
            {
                var sb = new StringBuilder(new_.Length * (s.Length + 1) + s.Length);
                sb.Append(new_);
                foreach (char c in s)
                {
                    sb.Append(c);
                    sb.Append(new_);
                }
                return sb.ToString();
            }
#pragma warning disable CA1307 // string.Replace(string, string, StringComparison) not available in netstandard2.0
            return s.Replace(old, new_);
#pragma warning restore CA1307
        }

        /// <summary>
        /// Return a copy with the first <paramref name="count"/> occurrences of
        /// <paramref name="old"/> replaced by <paramref name="new_"/>.
        /// Python: <c>str.replace(old, new, count)</c>
        /// </summary>
        public static string Replace(this string s, string old, string new_, int count)
        {
            if (count < 0)
            {
                return Replace(s, old, new_);
            }
            if (count == 0)
            {
                return s;
            }

            if (old.Length == 0)
            {
                var sb = new StringBuilder();
                int replacements = 0;
                if (replacements < count)
                {
                    sb.Append(new_);
                    replacements++;
                }
                foreach (char c in s)
                {
                    sb.Append(c);
                    if (replacements < count)
                    {
                        sb.Append(new_);
                        replacements++;
                    }
                }
                return sb.ToString();
            }

            var result = new StringBuilder(s.Length);
            int start = 0;
            int replaced = 0;

            while (start < s.Length && replaced < count)
            {
                int index = s.IndexOf(old, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }
                result.Append(s, start, index - start);
                result.Append(new_);
                start = index + old.Length;
                replaced++;
            }
            result.Append(s, start, s.Length - start);
            return result.ToString();
        }
    }
}
