using System.Collections;

namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        return new ListIterator<T>(this);
    }

    /// <summary>
    /// Non-generic enumerator for IEnumerable interface.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
