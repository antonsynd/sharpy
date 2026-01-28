namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <summary>
        /// Returns a hash code for this set.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + typeof(Set<T>).GetHashCode();
                hash = hash * 31 + _set.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Deprecated: Use <see cref="GetHashCode()"/> instead.
        /// </summary>
        public int __Hash__() => GetHashCode();
    }
}
