using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Format, maketrans, and translate methods for Str.
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
            return StringExtensions.Maketrans(x, y);
        }

        /// <summary>
        /// Build a translation table with a deletion set.
        /// Python: <c>str.maketrans(x, y, z)</c>
        /// </summary>
        public static Dictionary<char, string> Maketrans(string x, string y, string z)
        {
            return StringExtensions.Maketrans(x, y, z);
        }

        /// <summary>
        /// Return a copy of the string in which each character has been mapped
        /// through the given translation table.
        /// Python: <c>str.translate(table)</c>
        /// </summary>
        public Str Translate(Dictionary<char, string> table)
        {
            return new Str(StringExtensions.Translate(Value, table));
        }

        /// <summary>
        /// Encode the string using the specified encoding and return as bytes.
        /// Python: <c>str.encode(encoding='utf-8')</c>
        /// </summary>
        public Bytes Encode(string encoding = "utf-8")
        {
            return new Bytes(StringExtensions.Encode(Value, encoding));
        }
    }
}
