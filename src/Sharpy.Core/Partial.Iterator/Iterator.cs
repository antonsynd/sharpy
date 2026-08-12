using System.Collections.Generic;
namespace Sharpy
{
    /// <summary>
    /// An object representing a stream of data. Repeated calls to the
    /// iterator's <see cref="Next()"/> method return successive items in the
    /// stream. When no more data are available, a <see cref="StopIteration"/>
    /// exception is raised instead. At this point, the iterator object is
    /// exhausted and any further calls to its <see cref="Next()"/> method
    /// just raise <see cref="StopIteration"/> again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Iterators implement the .NET enumeration protocol (via <see cref="IEnumerator{T}"/>). Use
    /// <see cref="MoveNext()"/> and <see cref="Current"/> for .NET-style iteration,
    /// or <see cref="Next()"/> for Sharpy-style iteration.
    /// </para>
    /// <para>
    /// Iterators return themselves from <see cref="GetEnumerator()"/> so they can be
    /// used in foreach loops. Note that iterators are single-pass - once exhausted,
    /// they cannot be reset.
    /// </para>
    /// </remarks>
    public abstract partial class Iterator<T> : IEnumerator<T>, IEnumerable<T>
    {
        /// <summary>
        /// Return the next item from the iterator. If there are no further
        /// items, raises the <see cref="StopIteration"/> exception.
        /// </summary>
        public T Next()
        {
            if (MoveNext())
            {
                return Current;
            }

            throw new StopIteration();
        }

        /// <summary>
        /// Advances the enumerator to the next element of the collection.
        /// Subclasses must set <see cref="_current"/> when returning true.
        /// </summary>
        /// <returns>true if the enumerator was successfully advanced to the next element;
        /// false if the enumerator has passed the end of the collection.</returns>
        public abstract bool MoveNext();

        /// <summary>
        /// A Python-shaped rendering of the iterator. Subclasses override with their own name.
        /// </summary>
        /// <remarks>
        /// Without this, printing an iterator rendered .NET's default — the raw CLR type name, e.g.
        /// <c>Sharpy.MapIterator`2[System.Int32,System.Int32]</c> — which tells a Python reader
        /// nothing (#1354).
        /// <para>
        /// CPython's form for every iterator here EXCEPT <c>range</c> embeds the object's address
        /// (<c>&lt;map object at 0x102...&gt;</c>). An address can never match byte-for-byte, so
        /// these deliberately drop the <c>at 0x...</c> tail and are recorded in
        /// <c>docs/deviations.yaml</c>. Dropping it is what makes the value pinnable at all; keeping
        /// it would reproduce exactly the untestable-repr class that #1392 removed from frozendict.
        /// <c>range</c> is not a deviation — CPython's <c>range(0, 3)</c> carries no address and is
        /// matched exactly.
        /// </para>
        /// </remarks>
        public override string ToString() => "<iterator object>";
    }
}
