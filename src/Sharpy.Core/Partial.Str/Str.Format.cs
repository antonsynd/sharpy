using System;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Format, maketrans, translate, and encode methods for Str.
    /// </summary>
    public readonly partial struct Str
    {
        /// <summary>
        /// Build a translation table mapping characters in <paramref name="x"/>
        /// to corresponding characters in <paramref name="y"/>.
        /// Python: <c>str.maketrans(x, y)</c>
        /// </summary>
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
        /// Build a translation table with a deletion set.
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
        /// through the given translation table.
        /// Python: <c>str.translate(table)</c>
        /// </summary>
        public Str Translate(Dictionary<char, string> table)
        {
            var sb = new StringBuilder(Value.Length);
            foreach (char c in Value)
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
            return new Str(sb.ToString());
        }

        /// <summary>
        /// Encode the string using the specified encoding and return as bytes.
        /// Python: <c>str.encode(encoding='utf-8')</c>
        /// </summary>
        public Bytes Encode(string encoding = "utf-8")
        {
#pragma warning disable CA1307 // string.Replace(string, string, StringComparison) not available in netstandard2.0
            switch (encoding.ToLowerInvariant().Replace("-", "").Replace("_", ""))
#pragma warning restore CA1307
            {
                case "utf8":
                    return new Bytes(Encoding.UTF8.GetBytes(Value));
                case "ascii":
                    return new Bytes(Encoding.ASCII.GetBytes(Value));
                case "utf16":
                case "utf16le":
                    return new Bytes(Encoding.Unicode.GetBytes(Value));
                case "utf16be":
                    return new Bytes(Encoding.BigEndianUnicode.GetBytes(Value));
                case "utf32":
                    return new Bytes(Encoding.UTF32.GetBytes(Value));
                case "latin1":
                case "iso88591":
                    return new Bytes(Encoding.GetEncoding("iso-8859-1").GetBytes(Value));
                default:
                    throw new LookupError("unknown encoding: " + encoding);
            }
        }
    }
}
