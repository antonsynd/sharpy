namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public int __Id__()
    {
        return GetHashCode();
    }
}
