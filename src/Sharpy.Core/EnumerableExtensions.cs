using System.Collections.Generic;
namespace Sharpy.Core
{
    /// <summary>
    /// Extension methods to bridge C# IEnumerable with Sharpy's iterator protocol.
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Converts any C# IEnumerable to a Sharpy Iterator.
        /// </summary>
        /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
        /// <param name="enumerable">The C# enumerable to convert.</param>
        /// <returns>A Sharpy Iterator that wraps the enumerable.</returns>
        /// <exception cref="TypeError">Thrown when enumerable is null.</exception>
        public static Iterator<T> ToIterator<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable is null)
            {
                throw TypeError.ArgNone("iter", "enumerable");
            }

            // Wrap the enumerator
            return new EnumeratorIterator<T>(enumerable.GetEnumerator());
        }
    }
}
