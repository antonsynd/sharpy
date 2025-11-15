namespace Sharpy.Core;

public sealed partial class Set<T>
{
    public bool IsReadOnly
    {
        get
        {
            return false;
        }
    }

    bool System.Collections.Generic.ICollection<T>.Remove(T item)
    {
        return _set.Remove(item);
    }

    bool Collections.Interfaces.ICollection<T>.Remove(T item)
    {
        return _set.Remove(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _set.CopyTo(array, arrayIndex);
    }
}
