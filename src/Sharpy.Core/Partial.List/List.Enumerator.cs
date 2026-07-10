using System;
using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    public sealed partial class List<T>
    {
        /// <summary>
        /// Allocation-free forward enumerator over a concrete <see cref="List{T}"/>.
        /// Bound by <c>foreach</c> on the concrete type; interface-based iteration
        /// still returns the class <see cref="ListIterator{T}"/> for Python-protocol
        /// semantics.
        /// </summary>
        /// <remarks>
        /// Mirrors <see cref="ListIterator{T}"/>: it indexes the backing store live,
        /// so it reflects (rather than guards against) mutation during iteration.
        /// </remarks>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly System.Collections.Generic.List<T> _backing;
            private int _index;
            private T? _current;

            internal Enumerator(List<T> list)
            {
                _backing = list._list;
                _index = 0;
                _current = default;
            }

            /// <inheritdoc/>
            public T Current => _current!;

            /// <inheritdoc/>
            object? IEnumerator.Current => Current;

            /// <inheritdoc/>
            public bool MoveNext()
            {
                var backing = _backing;

                if (_index < backing.Count)
                {
                    _current = backing[_index];
                    ++_index;
                    return true;
                }

                _current = default;
                return false;
            }

            /// <summary>
            /// Not supported: mirrors <see cref="ListIterator{T}"/>, which cannot
            /// be reset.
            /// </summary>
            /// <exception cref="NotSupportedException">Always thrown.</exception>
            public void Reset()
            {
                throw new NotSupportedException("Iterators cannot be reset. Create a new iterator instead.");
            }

            /// <inheritdoc/>
            public void Dispose()
            {
            }
        }
    }
}
