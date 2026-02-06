namespace Sharpy
{
    /// <summary>
    /// Operator overloads for Set&lt;T&gt;.
    /// Includes equality, subset/superset comparison, set operations, and truthiness operators.
    /// </summary>
    public sealed partial class Set<T>
    {
        #region Equality Operators

        /// <summary>
        /// Determines whether two sets are equal by comparing elements.
        /// </summary>
        /// <remarks>
        /// Returns true for both sets if they contain the same items,
        /// even if they are not the same set reference.
        /// </remarks>
        public static bool operator ==(Set<T>? left, Set<T>? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        /// <summary>
        /// Determines whether two sets are not equal.
        /// </summary>
        public static bool operator !=(Set<T>? left, Set<T>? right)
        {
            return !(left == right);
        }

        #endregion

        #region Subset/Superset Operators

        /// <summary>
        /// Determines whether the left set is a proper subset of the right set.
        /// </summary>
        public static bool operator <(Set<T> left, Set<T> right)
        {
            return left.IsProperSubset(right);
        }

        /// <summary>
        /// Determines whether the left set is a subset of the right set.
        /// </summary>
        public static bool operator <=(Set<T> left, Set<T> right)
        {
            return left.IsSubset(right);
        }

        /// <summary>
        /// Determines whether the left set is a proper superset of the right set.
        /// </summary>
        public static bool operator >(Set<T> left, Set<T> right)
        {
            return left.IsProperSuperset(right);
        }

        /// <summary>
        /// Determines whether the left set is a superset of the right set.
        /// </summary>
        public static bool operator >=(Set<T> left, Set<T> right)
        {
            return left.IsSuperset(right);
        }

        #endregion

        #region Set Operation Operators

        /// <summary>
        /// Returns a new set containing elements from both sets (union).
        /// </summary>
        public static Set<T> operator |(Set<T> left, Set<T> right)
        {
            return left.Union(right);
        }

        /// <summary>
        /// Returns a new set containing elements common to both sets (intersection).
        /// </summary>
        public static Set<T> operator &(Set<T> left, Set<T> right)
        {
            return left.Intersection(right);
        }

        /// <summary>
        /// Returns a new set containing elements in either set but not both (symmetric difference).
        /// </summary>
        public static Set<T> operator ^(Set<T> left, Set<T> right)
        {
            return left.SymmetricDifference(right);
        }

        /// <summary>
        /// Returns a new set containing elements in the left set but not in the right set (difference).
        /// </summary>
        public static Set<T> operator -(Set<T> left, Set<T> right)
        {
            return left.Difference(right);
        }

        #endregion

        #region Truthiness Operators

        /// <summary>
        /// Returns true if the set is not null and not empty.
        /// </summary>
        public static bool operator true(Set<T>? set)
        {
            return set is not null && set._set.Count > 0;
        }

        /// <summary>
        /// Returns true if the set is null or empty.
        /// </summary>
        public static bool operator false(Set<T>? set)
        {
            return set is null || set._set.Count == 0;
        }

        #endregion
    }
}
