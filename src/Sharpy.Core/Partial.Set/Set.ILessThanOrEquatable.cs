namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <summary>
        /// Deprecated: Use <see cref="IsSubset(Set{T})"/> instead.
        /// </summary>
        public bool __Le__(Set<T> other)
        {
            return IsSubset(other);
        }
    }
}
