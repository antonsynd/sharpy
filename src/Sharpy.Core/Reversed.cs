using System.Collections.Generic;
using System.Linq;

namespace Sharpy.Core
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a reverse iterator over the values of the given sequence.
        /// </summary>
        /// <remarks>
        /// For <see cref="IList{T}"/> implementations, iterates backwards efficiently.
        /// For other sequences, materializes the sequence and reverses using LINQ.
        /// </remarks>
        public static Iterator<T> Reversed<T>(IEnumerable<T> sequence)
        {
            if (sequence is null)
            {
                throw TypeError.ArgNone("reversed", "sequence");
            }

            // Optimization for IList<T>: iterate backwards without materializing
            if (sequence is IList<T> list)
            {
                return new ListReverseEnumerator<T>(list);
            }

            // Fallback: materialize and reverse using LINQ
            return new EnumeratorIterator<T>(sequence.Reverse().GetEnumerator());
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

        public override T __Next__()
        {
            if (_index < 0)
            {
                throw new StopIteration();
            }

            return _list[_index--];
        }
    }
}
