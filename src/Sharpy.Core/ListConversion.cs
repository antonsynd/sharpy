using System.Collections.Generic;
using System;

namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for list
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert IEnumerable to list
        /// </summary>
        public static List<T> List<T>(IEnumerable<T> enumerable)
        {
            return new List<T>(enumerable);
        }

        /// <summary>
        /// Create empty list
        /// </summary>
        public static List<T> List<T>()
        {
            return new List<T>();
        }

        /// <summary>
        /// Convert list to list (copy)
        /// </summary>
        public static List<T> List<T>(List<T> other)
        {
            return other.Copy();
        }

        /// <summary>
        /// Builds a list of single-character strings from a string, matching Python's
        /// <c>list("abc")</c> -> <c>['a', 'b', 'c']</c> and <c>list("")</c> -> <c>[]</c>.
        /// Iterates by UTF-16 code unit (Axiom 1), consistent with
        /// <see cref="StringHelpers.Iterate"/>: <c>list("abc")</c> selects this overload because
        /// C# would otherwise bind <c>list(string)</c> to <c>List&lt;char&gt;</c>
        /// (<c>string</c> is <c>IEnumerable&lt;char&gt;</c>), diverging from Python (#1067).
        /// </summary>
        public static List<string> ListFromStr(string s)
        {
            if (s is null)
            {
                throw TypeError.ArgNone("list", "iterable");
            }

            var result = new List<string>(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                result.Add(s[i].ToString());
            }
            return result;
        }
    }
}
