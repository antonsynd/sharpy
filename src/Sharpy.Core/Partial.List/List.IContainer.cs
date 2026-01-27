namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Returns whether the item is in the list.
    /// </summary>
    /// <remarks>
    /// This is the implementation for <see cref="ICollection{T}.Contains(T)"/>.
    /// </remarks>
    public bool Contains(T x)
    {
        return _list.Contains(x);
    }

    /// <summary>
    /// Returns whether the item is in the list.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <see cref="Contains(T)"/> instead.
    /// </remarks>
    public bool __Contains__(T x)
    {
        return Contains(x);
    }
}
