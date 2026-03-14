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
    public static partial class StringExtensions
    {
        // ----------------------------------------------------------------
        // Priority 1 — core methods
        // ----------------------------------------------------------------

        /// <summary>
        /// Return a copy of the string converted to uppercase.
        /// Python: <c>str.upper()</c>
        /// </summary>
        /// <remarks>Uses invariant culture to match Python's culture-independent behavior.</remarks>
        /// <example>
        /// <code>
        /// "hello".upper()    # "HELLO"
        /// </code>
        /// </example>
        public static string Upper(this string s)
        {
            return s.ToUpperInvariant();
        }

        /// <summary>
        /// Return a copy of the string converted to lowercase.
        /// Python: <c>str.lower()</c>
        /// </summary>
        /// <remarks>Uses invariant culture to match Python's culture-independent behavior.</remarks>
        /// <example>
        /// <code>
        /// "HELLO".lower()    # "hello"
        /// </code>
        /// </example>
        public static string Lower(this string s)
        {
            return s.ToLowerInvariant();
        }

        /// <summary>
        /// Return a copy of the string with leading and trailing whitespace removed.
        /// Python: <c>str.strip()</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "  hello  ".strip()    # "hello"
        /// </code>
        /// </example>
        public static string Strip(this string s)
        {
            return s.Trim();
        }

        /// <summary>
        /// Return a copy of the string with leading and trailing characters in
        /// <paramref name="chars"/> removed.
        /// Python: <c>str.strip(chars)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "xxhelloxx".strip("x")    # "hello"
        /// </code>
        /// </example>
        public static string Strip(this string s, string chars)
        {
            return s.Trim(chars.ToCharArray());
        }

        /// <summary>
        /// Return a copy of the string with leading whitespace removed.
        /// Python: <c>str.lstrip()</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "  hello".lstrip()    # "hello"
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "hello  ".rstrip()    # "hello"
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "hello world".capitalize()    # "Hello world"
        /// </code>
        /// </example>
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
        /// Return a string which is the concatenation of the strings in
        /// <paramref name="iterable"/>. The separator between elements is the
        /// string providing this method.
        /// Python: <c>str.join(iterable)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// ", ".join(["a", "b", "c"])    # "a, b, c"
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "hello world".title()    # "Hello World"
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "Hello World".swapcase()    # "hELLO wORLD"
        /// </code>
        /// </example>
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
        /// Return centered in a string of length <paramref name="width"/>.
        /// Padding is done using the specified <paramref name="fillchar"/>
        /// (default is a space).
        /// Python: <c>str.center(width, fillchar)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "hi".center(10)         # "    hi    "
        /// "hi".center(10, "-")    # "----hi----"
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "42".zfill(5)     # "00042"
        /// "-42".zfill(5)    # "-0042"
        /// </code>
        /// </example>
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
        /// If the string starts with the <paramref name="prefix"/> string,
        /// return <c>string[len(prefix):]</c>. Otherwise, return a copy of
        /// the original string.
        /// Python: <c>str.removeprefix(prefix)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "HelloWorld".removeprefix("Hello")    # "World"
        /// "HelloWorld".removeprefix("Bye")      # "HelloWorld"
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// "HelloWorld".removesuffix("World")    # "Hello"
        /// "HelloWorld".removesuffix("Bye")      # "HelloWorld"
        /// </code>
        /// </example>
        public static string Removesuffix(this string s, string suffix)
        {
            if (s.EndsWith(suffix, StringComparison.Ordinal))
            {
                return s.Substring(0, s.Length - suffix.Length);
            }

            return s;
        }

        // ----------------------------------------------------------------
        // Replace
        // ----------------------------------------------------------------

        /// <summary>
        /// Return a copy with all occurrences of <paramref name="old"/> replaced
        /// by <paramref name="new_"/>.
        /// Python: <c>str.replace(old, new)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "hello world".replace("world", "there")    # "hello there"
        /// </code>
        /// </example>
        public static string Replace(this string s, string old, string new_)
        {
            if (old.Length == 0)
            {
                // Python inserts new_ between every character and at start/end
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
                // Python inserts new_ between chars, limited by count
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
