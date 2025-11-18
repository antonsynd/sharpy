namespace Sharpy.Core;

using System.Collections;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        // Delegate to __Iter__() to ensure consistent iteration behavior
        return __Iter__();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
