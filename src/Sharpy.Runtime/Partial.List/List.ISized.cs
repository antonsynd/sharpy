namespace Sharpy;

public sealed partial class List<T>
{
    /// <summary>
    /// Returns the number of items in the list.
    /// </summary>
    public uint __Len__()
    {
        return (uint)_list.Count;
    }
}
