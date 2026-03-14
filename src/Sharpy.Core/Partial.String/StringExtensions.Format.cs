using System;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Formatting, encoding, and translation methods for StringExtensions:
    /// Expandtabs, Istitle, Encode, Maketrans, Translate.
    /// </summary>
    public static partial class StringExtensions
    {
        // ----------------------------------------------------------------
        // Expandtabs / Istitle / Encode
        // ----------------------------------------------------------------

        /// <summary>
        /// Return a copy where all tab characters are expanded using spaces.
        /// The column position is tracked; tab stops are at every
        /// <paramref name="tabsize"/> characters.
        /// Python: <c>str.expandtabs(tabsize=8)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "a\tb".expandtabs(4)    # "a   b"
        /// </code>
        /// </example>
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
                        // No expansion
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
        /// Return <c>true</c> if the string is a titlecased string and there is
        /// at least one character. Uppercase characters may only follow uncased
        /// characters and lowercase characters only cased characters.
        /// Python: <c>str.istitle()</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "Hello World".istitle()    # True
        /// "hello world".istitle()    # False
        /// </code>
        /// </example>
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
        /// Encode the string using the specified encoding and return as a byte array.
        /// Python: <c>str.encode(encoding='utf-8')</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "hello".encode()           # b'hello'  (UTF-8)
        /// "hello".encode("ascii")    # b'hello'  (ASCII)
        /// </code>
        /// </example>
        public static byte[] Encode(this string s, string encoding = "utf-8")
        {
#pragma warning disable CA1307 // string.Replace(string, string, StringComparison) not available in netstandard2.0
            switch (encoding.ToLowerInvariant().Replace("-", "").Replace("_", ""))
#pragma warning restore CA1307
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
                case "utf32":
                    return Encoding.UTF32.GetBytes(s);
                case "latin1":
                case "iso88591":
                    return Encoding.GetEncoding("iso-8859-1").GetBytes(s);
                default:
                    throw new LookupError("unknown encoding: " + encoding);
            }
        }

        // ----------------------------------------------------------------
        // Maketrans / Translate
        // ----------------------------------------------------------------

        /// <summary>
        /// Build a translation table mapping characters in <paramref name="x"/>
        /// to corresponding characters in <paramref name="y"/>.
        /// Python: <c>str.maketrans(x, y)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// t = str.maketrans("aeiou", "12345")
        /// "apple".translate(t)    # "1ppl2"
        /// </code>
        /// </example>
        public static Dictionary<char, string> Maketrans(string x, string y)
        {
            if (x.Length != y.Length)
            {
                throw new ValueError("the first two maketrans arguments must have equal length");
            }
            var table = new Dictionary<char, string>(x.Length);
            for (int i = 0; i < x.Length; i++)
            {
                table[x[i]] = y[i].ToString();
            }
            return table;
        }

        /// <summary>
        /// Build a translation table mapping characters in <paramref name="x"/>
        /// to corresponding characters in <paramref name="y"/>, and mapping
        /// each character in <paramref name="z"/> to deletion (empty string).
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
        /// through the given translation table. Characters mapped to an empty
        /// string are deleted.
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
    }
}
