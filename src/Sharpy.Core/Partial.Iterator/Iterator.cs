namespace Sharpy.Core;

using Collections.Interfaces;

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
/// Iterators implement both Sharpy's iterator protocol (via <see cref="IIterable{T}"/>)
/// and .NET's enumeration protocol (via <see cref="IEnumerator{T}"/>). Use
/// <see cref="MoveNext()"/> and <see cref="Current"/> for .NET-style iteration,
/// or <see cref="Next()"/> for Sharpy-style iteration.
/// </para>
/// <para>
/// Iterators return themselves from <see cref="GetEnumerator()"/> so they can be
/// used in foreach loops. Note that iterators are single-pass - once exhausted,
/// they cannot be reset.
/// </para>
/// </remarks>
public abstract partial class Iterator<T> : IIterable<T>, IEnumerator<T>
{
    /// <summary>
    /// Return the next item from the iterator. If there are no further
    /// items, raises the <see cref="StopIteration"/> exception.
    /// </summary>
    public T Next() => __Next__();

    /// <summary>
    /// Deprecated: Use <see cref="Next()"/> instead.
    /// Return the next item from the iterator. If there are no further
    /// items, raises the <see cref="StopIteration"/> exception.
    /// </summary>
    public abstract T __Next__();
}
