using System.Collections;

namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        // Delegate to __Iter__() to ensure consistent iteration behavior
        return __Iter__();
    }

    /// <summary>
    /// Delegate to specialized GetEnumerator() for generalized one.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
