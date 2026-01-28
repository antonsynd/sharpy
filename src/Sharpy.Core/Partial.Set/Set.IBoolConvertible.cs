namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <summary>
    /// Returns true if the set is not empty.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <c>set</c> in a boolean context (operator true/false) instead.
    /// </remarks>
    public override bool __Bool__()
    {
        return _set.Count > 0;
    }
}
