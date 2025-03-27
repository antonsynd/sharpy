namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public override bool __Bool__()
        {
            return _set.Count > 0;
        }

        public override int __Hash__()
        {
            var hashCode = new HashCode();
            hashCode.Add(typeof(Set<T>).GetHashCode());
            hashCode.Add(_set.GetHashCode());

            return hashCode.ToHashCode();
        }
    }
}
