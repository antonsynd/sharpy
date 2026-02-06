using System.Collections.Generic;
namespace Sharpy
{
    /// <summary>
    /// Iterator that yields (index, value) tuples from an iterable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable</typeparam>
    public class EnumerateIterator<T> : Iterator<(int, T)>
    {
        private readonly Iterator<T> _iterator;
        private int _index;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumerateIterator{T}"/> class.
        /// </summary>
        /// <param name="iterable">The iterable to enumerate</param>
        /// <param name="start">The starting index</param>
        public EnumerateIterator(IEnumerable<T> iterable, int start = 0)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("enumerate", "iterable");
            }

            _iterator = Builtins.Iter(iterable);
            _index = start;
        }

        /// <inheritdoc/>
        public override (int, T) __Next__()
        {
            var value = _iterator.__Next__();
            var result = (_index, value);
            _index++;
            return result;
        }
    }

    public static partial class Builtins
    {
        /// <summary>
        /// Return an enumerate object. The iterable must be a sequence, an iterator,
        /// or some other object which supports iteration. The elements produced by
        /// enumerate are tuples containing a count (from start which defaults to 0)
        /// and the values obtained from iterating over iterable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <param name="iterable">The iterable to enumerate</param>
        /// <param name="start">The starting index (default 0)</param>
        /// <returns>An enumerate iterator</returns>
        public static EnumerateIterator<T> Enumerate<T>(IEnumerable<T> iterable, int start = 0)
        {
            return new EnumerateIterator<T>(iterable, start);
        }
    }
}
