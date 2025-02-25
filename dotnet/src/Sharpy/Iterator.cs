using System.Collections;
using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    /// <summary>
    /// An object representing a stream of data. Repeated calls to the
    /// iterator’s <see cref="__Next__()"/> method (or passing it to the
    /// built-in function <see cref="Next()"/>) return successive items in the
    /// stream. When no more data are available, a <see cref="StopIteration"/>
    /// exception is raised instead. At this point, the iterator object is
    /// exhausted and any further calls to its <see cref="__Next__()"/> method
    /// just raise <see cref="StopIteration"/> again. Iterators are required
    /// to have an <see cref="__Iter__()"/> method that returns the iterator
    /// object itself so every iterator is also iterable and may be used in
    /// most places where other iterables are accepted. One notable exception
    /// is code which attempts multiple iteration passes. A container object
    /// (such as a list) produces a fresh new iterator each time you pass it
    /// to the <see cref="Iter()"/> function or use it in a <c>for</c> loop.
    /// Attempting this with an iterator will just return the same exhausted
    /// iterator object used in the previous iteration pass, making it appear
    /// like an empty container.
    /// </summary>
    public abstract class Iterator<T> : Iterable<T>
    {
        /// <summary>
        /// Return the iterator object itself. This is required to allow both
        /// containers and iterators to be used with the <c>for</c> and
        /// <c>in</c> statements.
        /// </summary>
        public virtual Iterator<T> __Iter__()
        {
            return this;
        }

        /// <summary>
        /// Return the next item from the iterator. If there are no further
        /// items, raises the <see cref="StopIteration"/> exception.
        /// </summary>
        public abstract T __Next__();

        public abstract IEnumerator<T> GetEnumerator();

        /// <remarks>
        /// Type-erased version of <see cref="GetEnumerator()"/>.
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
