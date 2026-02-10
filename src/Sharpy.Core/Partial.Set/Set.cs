using System.Collections.Generic;
namespace Sharpy
{
    public sealed partial class Set<T>
        : System.Collections.Generic.ISet<T>,
          System.IEquatable<Set<T>>,
          ISized
    {
        // Internal for SetIterator access to avoid infinite recursion when GetEnumerator delegates to __Iter__
        internal readonly HashSet<T> _set;

        public Set()
        {
            _set = new HashSet<T>();
        }

        public Set(Set<T> set) : this()
        {
            _set.UnionWith(set._set);
        }

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

        public Set<T> Copy()
        {
            var newSet = new Set<T>();
            newSet._set.UnionWith(_set);

            return newSet;
        }

        /// <summary>
        /// Returns whether this set is a proper subset of other (subset but not equal).
        /// </summary>
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

        public HashSet<T> ToHashSet()
        {
            return new HashSet<T>(_set);
        }
    }
}
