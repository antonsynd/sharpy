namespace Sharpy.Core;

public sealed partial class SetIterator<T> : Iterator<T>
{
    private readonly Set<T> _set;
    private readonly IEnumerator<T> _setEnumerator;

    internal SetIterator(Set<T> set)
    {
        _set = set;
        // Access the underlying HashSet directly to avoid infinite recursion
        // since Set.GetEnumerator() now delegates to __Iter__()
        _setEnumerator = set._set.GetEnumerator();
    }
}
