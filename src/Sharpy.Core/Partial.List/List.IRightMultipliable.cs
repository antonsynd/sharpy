namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Repeats this list a specified number of times, returning a new list.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <c>count * list</c> operator instead.
    /// </remarks>
    public List<T> __RMul__(int count)
    {
        return count * this;
    }
}
