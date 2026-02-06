using System.Collections.Generic;
using System.Collections;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the length (the number of items) of a collection.
        /// </summary>
        /// <remarks>
        /// Uses the non-generic <see cref="System.Collections.ICollection"/> interface
        /// which is implemented by arrays, List{T}, Dictionary{K,V}, etc.
        /// This avoids overload ambiguity when a type implements both
        /// <see cref="ICollection{T}"/> and <see cref="IReadOnlyCollection{T}"/>.
        /// </remarks>
        public static int Len(System.Collections.ICollection c)
        {
            if (c is null)
            {
                throw TypeError.ArgNone("len", "sized");
            }

            return c.Count;
        }

        /// <summary>
        /// Return the length of a string.
        /// </summary>
        public static int Len(string s)
        {
            return s.Length;
        }
    }
}
