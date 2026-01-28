namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <summary>
        /// Deprecated: Use <see cref="IsProperSubset(Set{T})"/> instead.
        /// </summary>
        public bool __Lt__(Set<T> other)
        {
            return IsProperSubset(other);
        }
    }
}
