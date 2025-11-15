namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public Iterator<T> __Iter__()
    {
        return new SetIterator<T>(this);
    }
}
