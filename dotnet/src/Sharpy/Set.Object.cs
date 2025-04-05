namespace Sharpy
{
    public sealed partial class Set<T>
    {
        /// <inheritdoc/>
        public override bool __Bool__()
        {
            return _set.Count > 0;
        }

        /// <inheritdoc/>
        public override int __Hash__()
        {
            var hashCode = new HashCode();
            hashCode.Add(typeof(Set<T>).GetHashCode());
            hashCode.Add(_set.GetHashCode());

            return hashCode.ToHashCode();
        }
    }
}
