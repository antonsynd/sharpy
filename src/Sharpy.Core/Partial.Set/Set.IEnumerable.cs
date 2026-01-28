namespace Sharpy.Core;

using System.Collections;

public sealed partial class Set<T>
{
    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    /// <remarks>
    /// This is the implementation for <see cref="IEnumerable{T}.GetEnumerator"/>.
    /// </remarks>
    public IEnumerator<T> GetEnumerator()
    {
        return new SetIterator<T>(this);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
