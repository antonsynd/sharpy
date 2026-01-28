namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <summary>
    /// Returns whether the item is in the set.
    /// </summary>
    /// <remarks>
    /// This is the implementation for <see cref="ICollection{T}.Contains(T)"/>.
    /// </remarks>
    public bool Contains(T x)
    {
        return _set.Contains(x);
    }

    /// <summary>
    /// Returns whether the item is in the set.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <see cref="Contains(T)"/> instead.
    /// </remarks>
    public bool __Contains__(T x)
    {
        return Contains(x);
    }
}
