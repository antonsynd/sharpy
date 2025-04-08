using System.Collections;

namespace Sharpy
{
    public sealed partial class List<T>
    {
        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator() { foreach (var elem in _list) { yield return elem; } }

        /// <summary>
        /// Delegate to specialized GetEnumerator() for generalized one.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
