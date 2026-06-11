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

        /// <summary>
        /// Return the length of a Sharpy list.
        /// </summary>
        /// <remarks>
        /// This concrete overload disambiguates between the
        /// <see cref="System.Collections.ICollection"/> and <see cref="ISized"/>
        /// overloads, both of which <see cref="Sharpy.List{T}"/> now satisfies (it
        /// implements the non-generic <see cref="System.Collections.IList"/>).
        /// An identity conversion to the concrete parameter type is preferred
        /// over the interface conversions, so this overload wins.
        /// </remarks>
        public static int Len<T>(List<T> list)
        {
            if (list is null)
            {
                throw TypeError.ArgNone("len", "sized");
            }

            return ((ISized)list).Count;
        }

        /// <summary>
        /// Return the length of a Sharpy dictionary.
        /// </summary>
        /// <remarks>
        /// This concrete overload disambiguates between the
        /// <see cref="System.Collections.ICollection"/> and <see cref="ISized"/>
        /// overloads, both of which <see cref="Dict{K, V}"/> now satisfies (it
        /// implements the non-generic <see cref="System.Collections.IDictionary"/>).
        /// </remarks>
        public static int Len<K, V>(Dict<K, V> dict)
            where K : notnull
        {
            if (dict is null)
            {
                throw TypeError.ArgNone("len", "sized");
            }

            return ((ISized)dict).Count;
        }

        /// <summary>
        /// Return the length of a string.
        /// </summary>
        public static int Len(string s)
        {
            if (s is null)
            {
                throw TypeError.ArgNone("len", "sized");
            }

            return s.Length;
        }

        /// <summary>
        /// Return the number of elements in a tuple.
        /// </summary>
        /// <remarks>
        /// Tuples are emitted as <see cref="System.ValueTuple"/> instances, which
        /// implement <see cref="System.Runtime.CompilerServices.ITuple"/> but
        /// neither <see cref="System.Collections.ICollection"/> nor
        /// <see cref="ISized"/>. This overload routes <c>len(tuple)</c> to
        /// <see cref="System.Runtime.CompilerServices.ITuple.Length"/>.
        /// </remarks>
        public static int Len(System.Runtime.CompilerServices.ITuple tuple)
        {
            if (tuple is null)
            {
                throw TypeError.ArgNone("len", "sized");
            }

            return tuple.Length;
        }
    }
}
