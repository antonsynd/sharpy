namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <summary>
        /// Determines whether this set equals another set by comparing elements.
        /// </summary>
        public bool Equals(Set<T>? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return _set.SetEquals(other._set);
        }

        /// <summary>
        /// Deprecated: Use <see cref="Equals(Set{T}?)"/> instead.
        /// </summary>
        public bool __Eq__(Set<T> other)
        {
            return Equals(other);
        }

        /// <summary>
        /// Determines whether this set is equal to the specified object.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is Set<T> set)
            {
                return Equals(set);
            }

            return false;
        }

        /// <summary>
        /// Deprecated: Use <see cref="Equals(object?)"/> instead.
        /// </summary>
        public bool __Eq__(object other)
        {
            return Equals(other);
        }
    }
}
