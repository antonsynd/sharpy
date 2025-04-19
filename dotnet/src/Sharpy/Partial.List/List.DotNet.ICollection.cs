namespace Sharpy;

public sealed partial class List<T>
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
        return _list.Remove(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _list.CopyTo(array, arrayIndex);
    }
}
