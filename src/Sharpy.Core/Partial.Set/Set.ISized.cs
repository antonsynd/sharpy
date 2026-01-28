namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <summary>
        /// Gets the number of elements in the set.
        /// </summary>
        public int Count => _set.Count;

        /// <summary>
        /// Deprecated: Use <see cref="Count"/> instead.
        /// </summary>
        public int __Len__()
        {
            return Count;
        }
    }
}
