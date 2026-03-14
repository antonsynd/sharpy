using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a reverse iterator over the values of the given sequence.
        /// </summary>
        /// <typeparam name="T">The type of elements in the sequence</typeparam>
        /// <param name="sequence">The sequence to reverse</param>
        /// <returns>An iterator that yields elements in reverse order</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="sequence"/> is null</exception>
        /// <remarks>
        /// For <see cref="IList{T}"/> implementations, iterates backwards efficiently.
        /// For other sequences, materializes the sequence and reverses using LINQ.
        /// </remarks>
        /// <example>
        /// <code>
        /// list(reversed([1, 2, 3]))    # [3, 2, 1]
        /// list(reversed("abc"))        # ["c", "b", "a"]
        /// </code>
        /// </example>
        public static Iterator<T> Reversed<T>(IEnumerable<T> sequence)
        {
            if (sequence is null)
            {
                throw TypeError.ArgNone("reversed", "sequence");
            }

            // Check for IReverseEnumerable<T> at runtime (user-defined __reversed__)
            if (sequence is IReverseEnumerable<T> reversible)
            {
                return new EnumeratorIterator<T>(reversible.GetReverseEnumerator());
            }

            // Optimization for IList<T>: iterate backwards without materializing
            if (sequence is IList<T> list)
            {
                return new ListReverseEnumerator<T>(list);
            }

            // Fallback: materialize and reverse using LINQ
            return new EnumeratorIterator<T>(sequence.Reverse().GetEnumerator());
        }

        /// <summary>
        /// Return a reverse iterator for types that implement <see cref="IReverseEnumerable{T}"/>
        /// but not <see cref="IEnumerable{T}"/> (i.e., types with __reversed__ but no __iter__).
        /// </summary>
        public static Iterator<T> Reversed<T>(IReverseEnumerable<T> reversible)
        {
            if (reversible is null)
            {
                throw TypeError.ArgNone("reversed", "sequence");
            }

            return new EnumeratorIterator<T>(reversible.GetReverseEnumerator());
        }
    }

    /// <summary>
    /// Iterator that iterates over an IList in reverse order.
    /// </summary>
    internal sealed class ListReverseEnumerator<T> : Iterator<T>
    {
        private readonly IList<T> _list;
        private int _index;

        public ListReverseEnumerator(IList<T> list)
        {
            _list = list;
            _index = list.Count - 1;
        }

        public override bool MoveNext()
        {
            if (_index < 0)
            {
                _current = default;
                return false;
            }

            _current = _list[_index--];
            return true;
        }
    }
}
