namespace Sharpy.Core
{
    public sealed partial class List<T>
    {
        /// <summary>
        /// Returns an iterator over the list elements.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <see cref="GetEnumerator()"/> instead.
        /// </remarks>
        public Iterator<T> __Iter__()
        {
            return (Iterator<T>)GetEnumerator();
        }
    }
}
