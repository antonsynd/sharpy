using System.Collections;
using System.Collections.Generic;
using System;

namespace Sharpy.Core
{
    public abstract partial class Iterator<T>
    {
        private T? _current;

        /// <summary>
        /// Gets the element in the collection at the current position of the enumerator.
        /// </summary>
        public T Current => _current!;

        /// <summary>
        /// Type-erased version of <see cref="Current"/>.
        /// </summary>
        object? IEnumerator.Current => Current;

        /// <summary>
        /// Advances the enumerator to the next element of the collection.
        /// </summary>
        /// <returns>true if the enumerator was successfully advanced to the next element;
        /// false if the enumerator has passed the end of the collection.</returns>
        public bool MoveNext()
        {
            try
            {
                _current = __Next__();
                return true;
            }
            catch (StopIteration)
            {
                _current = default;
                return false;
            }
        }

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element
        /// in the collection. Not supported for iterators as they are single-pass.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown as iterators cannot be reset.</exception>
        public void Reset()
        {
            throw new NotSupportedException("Iterators cannot be reset. Create a new iterator instead.");
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            // Default implementation does nothing
            // Derived classes can override if they need to dispose resources
        }

        /// <summary>
        /// Gets an enumerator for this iterator. Since iterators are themselves enumerators,
        /// this returns the iterator itself.
        /// </summary>
        /// <returns>This iterator instance.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return this;
        }

        /// <remarks>
        /// Type-erased version of <see cref="GetEnumerator()"/>.
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
