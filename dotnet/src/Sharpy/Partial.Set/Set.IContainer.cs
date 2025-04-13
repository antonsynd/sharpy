namespace Sharpy;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public bool Contains(T x)
    {
        return __Contains__(x);
    }

    /// <inheritdoc/>
    public bool __Contains__(T x)
    {
        return _set.Contains(x);
    }
}
