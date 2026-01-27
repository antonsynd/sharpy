namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Returns the number of items in the list.
    /// </summary>
    public int __Len__()
    {
        return _list.Count;
    }
}
