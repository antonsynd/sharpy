using System.Collections;

namespace Sharpy
{
    public sealed partial class List<T>
    {
        public IEnumerator<T> GetEnumerator() { return null; }

        /// <summary>
        /// Delegate to specialized GetEnumerator() for generalized one.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
