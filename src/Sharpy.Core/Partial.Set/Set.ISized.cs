namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public int __Len__()
    {
        return _set.Count;
    }
}
