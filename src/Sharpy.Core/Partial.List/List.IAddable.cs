namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Concatenates this list with another list, returning a new list.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <c>list1 + list2</c> operator instead.
    /// </remarks>
    public List<T> __Add__(List<T> other)
    {
        return this + other;
    }
}
