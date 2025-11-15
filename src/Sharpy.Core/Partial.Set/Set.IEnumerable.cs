namespace Sharpy.Core;

using System.Collections;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var elem in _set)
        {
            yield return elem;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
