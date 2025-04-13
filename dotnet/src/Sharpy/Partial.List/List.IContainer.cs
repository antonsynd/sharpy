namespace Sharpy;
public sealed partial class List<T>
{
    /// <summary>
    /// Returns whether the item is in the list.
    /// </summary>
    public bool __Contains__(T x)
    {
        return _list.Contains(x);
    }

    /// <summary>
    /// Returns whether the item is in the list.
    /// </summary>
    public bool Contains(T x)
    {
        return __Contains__(x);
    }
}
