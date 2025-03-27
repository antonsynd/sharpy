using System.Collections;

namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public IEnumerator<T> GetEnumerator()
        {
            foreach (var elem in _set)
            {
                yield return elem;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
