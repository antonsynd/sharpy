using System.Collections.Generic;
namespace Sharpy
{
    /// <summary>
    /// A mutable set of unique elements, similar to Python's <c>set</c>.
    /// Supports set operations: union, intersection, difference, and symmetric difference.
    /// </summary>
    /// <typeparam name="T">The type of elements in the set</typeparam>
    public sealed partial class Set<T>
        : System.Collections.Generic.ISet<T>,
          System.IEquatable<Set<T>>,
          ISized
    {
        // Internal for SetIterator access to the underlying HashSet
        internal readonly HashSet<T> _set;

        /// <summary>Create an empty set.</summary>
        public Set()
        {
            _set = new HashSet<T>();
        }

        /// <summary>Create a set as a copy of another set.</summary>
        public Set(Set<T> set) : this()
        {
            _set.UnionWith(set._set);
        }

        /// <summary>Create a set from an iterable.</summary>
        public Set(IEnumerable<T> enumerable) : this()
        {
            if (enumerable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            foreach (var x in enumerable)
            {
                _set.Add(x);
            }
        }

        /// <summary>
        /// Return a shallow copy of the set.
        /// </summary>
        /// <returns>A new set with the same elements.</returns>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// t = s.copy()    # {1, 2, 3}
        /// </code>
        /// </example>
        public Set<T> Copy()
        {
            var newSet = new Set<T>();
            newSet._set.UnionWith(_set);

            return newSet;
        }

        /// <summary>
        /// Returns whether this set is a proper subset of other (subset but not equal).
        /// </summary>
        /// <param name="other">The set to compare against</param>
        /// <returns><c>true</c> if this set is a proper subset of <paramref name="other"/></returns>
        public bool IsProperSubset(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.IsProperSubsetOf(other._set);
        }

        /// <summary>
        /// Returns whether this set is a subset of other (all elements in other).
        /// </summary>
        /// <param name="other">The set to compare against.</param>
        /// <returns><c>true</c> if every element in this set is also in <paramref name="other"/>.</returns>
        /// <example>
        /// <code>
        /// a = {1, 2}
        /// b = {1, 2, 3}
        /// a.issubset(b)    # True
        /// </code>
        /// </example>
        public bool IsSubset(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.IsSubsetOf(other._set);
        }

        /// <summary>
        /// Returns whether this set is a proper superset of other (superset but not equal).
        /// </summary>
        /// <param name="other">The set to compare against</param>
        /// <returns><c>true</c> if this set is a proper superset of <paramref name="other"/></returns>
        public bool IsProperSuperset(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.IsProperSupersetOf(other._set);
        }

        /// <summary>
        /// Returns whether this set is a superset of other (contains all elements of other).
        /// </summary>
        /// <param name="other">The set to compare against.</param>
        /// <returns><c>true</c> if every element in <paramref name="other"/> is also in this set.</returns>
        /// <example>
        /// <code>
        /// a = {1, 2, 3}
        /// b = {1, 2}
        /// a.issuperset(b)    # True
        /// </code>
        /// </example>
        public bool IsSuperset(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.IsSupersetOf(other._set);
        }

        /// <summary>
        /// Returns a new set with elements from both sets.
        /// </summary>
        /// <param name="other">The other set.</param>
        /// <returns>A new set containing elements from both sets.</returns>
        /// <example>
        /// <code>
        /// a = {1, 2}
        /// b = {2, 3}
        /// a.union(b)    # {1, 2, 3}
        /// </code>
        /// </example>
        public Set<T> Union(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            var result = new Set<T>(_set);

            foreach (var item in other._set)
            {
                result._set.Add(item);
            }

            return result;
        }

        /// <summary>
        /// Returns a new set with elements common to both sets.
        /// </summary>
        /// <param name="other">The other set.</param>
        /// <returns>A new set containing only elements found in both sets.</returns>
        /// <example>
        /// <code>
        /// a = {1, 2, 3}
        /// b = {2, 3, 4}
        /// a.intersection(b)    # {2, 3}
        /// </code>
        /// </example>
        public Set<T> Intersection(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            var result = new Set<T>();

            foreach (var item in _set)
            {
                if (other._set.Contains(item))
                {
                    result._set.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a new set with elements in this set but not in other.
        /// </summary>
        /// <param name="other">The other set.</param>
        /// <returns>A new set with elements only in this set.</returns>
        /// <example>
        /// <code>
        /// a = {1, 2, 3}
        /// b = {2, 3, 4}
        /// a.difference(b)    # {1}
        /// </code>
        /// </example>
        public Set<T> Difference(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            var result = new Set<T>();

            foreach (var item in _set)
            {
                if (!other._set.Contains(item))
                {
                    result._set.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a new set with elements in either set but not both.
        /// </summary>
        /// <param name="other">The other set.</param>
        /// <returns>A new set with elements in exactly one of the two sets.</returns>
        /// <example>
        /// <code>
        /// a = {1, 2, 3}
        /// b = {2, 3, 4}
        /// a.symmetric_difference(b)    # {1, 4}
        /// </code>
        /// </example>
        public Set<T> SymmetricDifference(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            var result = new Set<T>();

            foreach (var item in _set)
            {
                if (!other._set.Contains(item))
                {
                    result._set.Add(item);
                }
            }

            foreach (var item in other._set)
            {
                if (!_set.Contains(item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>Convert to a standard .NET HashSet.</summary>
        public HashSet<T> ToHashSet()
        {
            return new HashSet<T>(_set);
        }
    }
}
