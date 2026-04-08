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
        /// <exception cref="TypeError">Thrown when <paramref name="c"/> is null</exception>
        /// <example>
        /// <code>
        /// len([1, 2, 3])    # 3
        /// len("hello")      # 5
        /// len({})           # 0
        /// </code>
        /// </example>
        public static int Len(System.Collections.ICollection c)
        {
            if (c is null)
            {
                throw TypeError.ArgNone("len", "sized");
            }

            return c.Count;
        }

        /// <summary>
        /// Return the length of an ISized type (user-defined types with __len__).
        /// </summary>
        /// <param name="sized">An object implementing <see cref="ISized"/></param>
        /// <returns>The number of elements</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="sized"/> is null</exception>
        public static int Len(ISized sized)
        {
            if (sized is null)
            {
                throw TypeError.ArgNone("len", "sized");
            }

            return sized.Count;
        }

        // Len(string) removed — Sharpy.Str implements ISized, so len() dispatches
        // through the ISized overload. The string overload would cause C# overload
        // ambiguity since Str has implicit conversion to string.
    }
}
