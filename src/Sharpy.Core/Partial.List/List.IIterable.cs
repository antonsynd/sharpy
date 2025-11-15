namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public Iterator<T> __Iter__()
    {
        return new ListIterator<T>(this);
    }
}
