namespace Sharpy.Core
{
    public sealed partial class List<T>
    {
        /// <summary>
        /// Returns a hash code for this list.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + typeof(List<T>).GetHashCode();
                hash = hash * 31 + _list.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Deprecated: Use <see cref="GetHashCode()"/> instead.
        /// </summary>
        public int __Hash__()
        {
            return GetHashCode();
        }
    }
}
