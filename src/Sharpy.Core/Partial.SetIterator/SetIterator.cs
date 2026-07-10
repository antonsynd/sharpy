using System.Collections.Generic;
namespace Sharpy
{
    /// <summary>Iterator for Set.</summary>
    public sealed partial class SetIterator<T> : Iterator<T>
    {
        private readonly Set<T> _set;

        // Hold the HashSet's struct enumerator by value rather than boxing it
        // behind IEnumerator<T>. Mutating calls (MoveNext) mutate the field in
        // place since it lives on this heap-allocated iterator.
        private HashSet<T>.Enumerator _setEnumerator;

        internal SetIterator(Set<T> set)
        {
            _set = set;
            // Access the underlying HashSet directly
            _setEnumerator = set._set.GetEnumerator();
        }
    }
}
