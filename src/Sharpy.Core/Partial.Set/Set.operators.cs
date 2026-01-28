using System.Linq;
namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <remarks>
        /// This returns true for both sets if they contain the same items,
        /// even if they are not the actual same set reference.
        /// </remarks>
        public static bool operator ==(Set<T>? left, Set<T>? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public static bool operator !=(Set<T>? left, Set<T>? right)
        {
            return !(left == right);
        }

        public static bool operator <(Set<T> left, Set<T> right)
        {
            return left.IsProperSubset(right);
        }

        public static bool operator >(Set<T> left, Set<T> right)
        {
            return left.IsProperSuperset(right);
        }

        public static bool operator <=(Set<T> left, Set<T> right)
        {
            return left.IsSubset(right);
        }

        public static bool operator >=(Set<T> left, Set<T> right)
        {
            return left.IsSuperset(right);
        }

        public static Set<T> operator |(Set<T> left, Set<T> right)
        {
            return left.Union(right);
        }

        public static Set<T> operator &(Set<T> left, Set<T> right)
        {
            return left.Intersection(right);
        }

        public static Set<T> operator ^(Set<T> left, Set<T> right)
        {
            return left.SymmetricDifference(right);
        }

        public static Set<T> operator -(Set<T> left, Set<T> right)
        {
            return left.Difference(right);
        }

        public static bool operator true(Set<T>? set)
        {
            return set is not null && set._set.Count > 0;
        }

        public static bool operator false(Set<T>? set)
        {
            return set is null || set._set.Count == 0;
        }
    }
}
