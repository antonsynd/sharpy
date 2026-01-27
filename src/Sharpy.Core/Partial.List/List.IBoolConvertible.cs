namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Returns true if the list is not empty.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <c>list</c> in a boolean context (operator true/false) instead.
    /// </remarks>
    public bool __Bool__()
    {
        return _list.Count > 0;
    }
}
