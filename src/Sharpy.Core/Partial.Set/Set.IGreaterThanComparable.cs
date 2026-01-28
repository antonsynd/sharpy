namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <summary>
    /// Deprecated: Use <see cref="IsProperSuperset(Set{T})"/> instead.
    /// </summary>
    public bool __Gt__(Set<T> other)
    {
        return IsProperSuperset(other);
    }
}
