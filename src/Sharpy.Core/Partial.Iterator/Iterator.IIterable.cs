namespace Sharpy.Core;

public abstract partial class Iterator<T>
{
    /// <summary>
    /// Deprecated: Use <see cref="GetEnumerator()"/> instead.
    /// Return the iterator object itself. This is required to allow both
    /// containers and iterators to be used with the <c>for</c> and <c>in</c>
    /// statements.
    /// </summary>
    public virtual Iterator<T> __Iter__()
    {
        return this;
    }
}
