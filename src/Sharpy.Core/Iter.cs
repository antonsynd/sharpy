using System.Collections.Generic;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return an iterator object from any C# enumerable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
        /// <param name="enumerable">The C# enumerable to get an iterator from.</param>
        /// <returns>An iterator for the enumerable.</returns>
        /// <exception cref="TypeError">Thrown when enumerable is null.</exception>
        /// <remarks>
        /// Wraps the enumerator using EnumeratorIterator.
        /// This allows any C# IEnumerable to work seamlessly with Sharpy's iterator protocol.
        /// </remarks>
        /// <example>
        /// <code>
        /// it = iter([1, 2, 3])
        /// next(it)    # 1
        /// next(it)    # 2
        /// </code>
        /// </example>
        public static Iterator<T> Iter<T>(IEnumerable<T> enumerable)
        {
            if (enumerable is null)
            {
                throw TypeError.ArgNone("iter", "enumerable");
            }

            // Optimization: if it's already an Iterator<T>, return it directly
            if (enumerable is Iterator<T> iterator)
            {
                return iterator;
            }

            // Wrap the enumerator using EnumeratorIterator
            return new EnumeratorIterator<T>(enumerable.GetEnumerator());
        }
    }
}
