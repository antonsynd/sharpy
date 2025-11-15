namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public Iterator<T> __Reversed__()
    {
        return new ListReverseIterator<T>(this);
    }
}
