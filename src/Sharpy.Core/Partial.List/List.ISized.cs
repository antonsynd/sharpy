namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Returns the number of items in the list.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use the Count property via IReadOnlyCollection{T} interface instead.
    /// Note: For counting occurrences of a specific item, use <c>list.Count(item)</c> method.
    /// </remarks>
    public int __Len__() => _list.Count;
}
