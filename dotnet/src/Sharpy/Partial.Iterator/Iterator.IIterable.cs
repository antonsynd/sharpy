namespace Sharpy;
public abstract partial class Iterator<T>
{
    /// <summary>
    /// Return the iterator object itself (a shallow copy). This is
    /// required to allow both containers and iterators to be used with
    /// the <c>for</c> and <c>in</c> statements.
    /// </summary>
    public virtual Iterator<T> __Iter__()
    {
        return this;
    }
}
