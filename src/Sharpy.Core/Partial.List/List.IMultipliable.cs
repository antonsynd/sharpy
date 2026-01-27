namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Repeats this list a specified number of times, returning a new list.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <c>list * count</c> operator instead.
    /// </remarks>
    public List<T> __Mul__(int count)
    {
        return this * count;
    }
}
