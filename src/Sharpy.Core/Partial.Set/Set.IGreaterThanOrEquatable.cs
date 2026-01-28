namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <summary>
        /// Deprecated: Use <see cref="IsSuperset(Set{T})"/> instead.
        /// </summary>
        public bool __Ge__(Set<T> other)
        {
            return IsSuperset(other);
        }
    }
}
