using System.Collections.Generic;
namespace Sharpy
{
    public sealed partial class SetIterator<T> : Iterator<T>
    {
        private readonly Set<T> _set;
        private readonly IEnumerator<T> _setEnumerator;

        internal SetIterator(Set<T> set)
        {
            _set = set;
            // Access the underlying HashSet directly
            _setEnumerator = set._set.GetEnumerator();
        }
    }
}
