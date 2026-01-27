namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Returns the number of items in the list.
    /// </summary>
    public int __Len__() => _list.Count;

    // The Count property is provided by ISized default interface implementation.
    // We cannot define a public Count property here because List<T> also has
    // a Count(T x) method (from ISequence) for counting occurrences of an item.
    // The ICollection<T>.Count interface requirement is satisfied by ISized.Count.
}
