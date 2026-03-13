using System.Collections;
using System.Collections.Generic;
using System;

namespace Sharpy
{
    /// <summary>Base class for all Sharpy iterators.</summary>
    public abstract partial class Iterator<T>
    {
        /// <summary>The current element. Set by subclasses in MoveNext.</summary>
        protected T? _current;

        /// <summary>
        /// Gets the element in the collection at the current position of the enumerator.
        /// </summary>
        public T Current => _current!;

        /// <summary>
        /// Type-erased version of <see cref="Current"/>.
        /// </summary>
        object? IEnumerator.Current => Current;

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
