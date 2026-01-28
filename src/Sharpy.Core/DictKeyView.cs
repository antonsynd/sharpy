using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy.Core
{
    using System.Collections;

    /// <summary>
    /// View of dictionary keys as a set-like object.
    /// This view reflects changes to the underlying dictionary.
    /// Supports set operations like intersection, union, difference.
    /// </summary>
    public sealed partial class DictKeyView<K, V>
        : IReadOnlyCollection<K>,
          System.IEquatable<Set<K>>
        where K : notnull
    {
        private readonly Dictionary<K, V>.KeyCollection _keys;

        internal DictKeyView(Dictionary<K, V>.KeyCollection keys)
        {
            _keys = keys;
        }

        /// <summary>
        /// Gets the number of keys in the view.
        /// </summary>
        public int Count => _keys.Count;

        /// <summary>
        /// Compares the count of this view to another set.
        /// </summary>
        public int CompareTo(Set<K>? other)
        {
            if (other == null)
                return 1;

            var thisCount = Count;
            var otherCount = other.Count;

            if (thisCount < otherCount)
                return -1;
            if (thisCount > otherCount)
                return 1;
            return 0;
        }

        /// <summary>
        /// Determines whether the view contains the specified key.
        /// </summary>
        public bool Contains(K x)
        {
            return _keys.Contains(x);
        }

        /// <summary>
        /// Determines whether this view equals another set (same elements).
        /// </summary>
        public bool Equals(Set<K>? other)
        {
            if (other is null)
                return false;

            if (Count != other.Count)
                return false;

            foreach (var key in _keys)
            {
                if (!other.Contains(key))
                    return false;
            }
            return true;
        }

        public IEnumerator<K> GetEnumerator()
        {
            foreach (var key in _keys)
            {
                yield return key;
            }
        }

        /// <summary>
        /// Return True if the view and other have a null intersection (no common elements).
        /// </summary>
        public bool IsDisjoint(Set<K> other)
        {
            foreach (var key in _keys)
            {
                if (other.Contains(key))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Return intersection with another set.
        /// </summary>
        public Set<K> Intersection(Set<K> other)
        {
            var result = new Set<K>();
            foreach (var key in _keys)
            {
                if (other.Contains(key))
                {
                    result.Add(key);
                }
            }
            return result;
        }

        /// <summary>
        /// Deprecated: Use <see cref="Intersection(Set{K})"/> instead.
        /// </summary>
        public Set<K> __And__(Set<K> other) => Intersection(other);

        /// <summary>
        /// Deprecated: Use <see cref="Contains(K)"/> instead.
        /// </summary>
        public bool __Contains__(K x) => Contains(x);

        /// <summary>
        /// Deprecated: Use <see cref="Equals(Set{K}?)"/> instead.
        /// </summary>
        public bool __Eq__(Set<K>? other) => Equals(other);

        /// <summary>
        /// Check if this is a superset or equal to other.
        /// </summary>
        public bool IsSuperset(Set<K> other)
        {
            return Equals(other) || IsProperSuperset(other);
        }

        /// <summary>
        /// Deprecated: Use <see cref="IsSuperset(Set{K})"/> instead.
        /// </summary>
        public bool __Ge__(Set<K> other) => IsSuperset(other);

        /// <summary>
        /// Check if this is a proper superset of other.
        /// </summary>
        public bool IsProperSuperset(Set<K> other)
        {
            if (Count <= other.Count)
            {
                return false;
            }

            // Check if all elements of other are in this
            foreach (var item in other)
            {
                if (!Contains(item))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Deprecated: Use <see cref="IsProperSuperset(Set{K})"/> instead.
        /// </summary>
        public bool __Gt__(Set<K> other) => IsProperSuperset(other);

        /// <summary>
        /// Deprecated: Use <see cref="GetEnumerator()"/> instead.
        /// </summary>
        public Iterator<K> __Iter__()
        {
            return new EnumeratorIterator<K>(GetEnumerator());
        }

        /// <summary>
        /// Deprecated: Use <see cref="Count"/> instead.
        /// </summary>
        public int __Len__() => Count;

        /// <summary>
        /// Check if this is a subset or equal to other.
        /// </summary>
        public bool IsSubset(Set<K> other)
        {
            if (Count > other.Count)
            {
                return false;
            }

            foreach (var key in _keys)
            {
                if (!other.Contains(key))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Deprecated: Use <see cref="IsSubset(Set{K})"/> instead.
        /// </summary>
        public bool __Le__(Set<K> other) => IsSubset(other);

        /// <summary>
        /// Check if this is a proper subset of other.
        /// </summary>
        public bool IsProperSubset(Set<K> other)
        {
            if (Count >= other.Count)
            {
                return false;
            }

            foreach (var key in _keys)
            {
                if (!other.Contains(key))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Deprecated: Use <see cref="IsProperSubset(Set{K})"/> instead.
        /// </summary>
        public bool __Lt__(Set<K> other) => IsProperSubset(other);

        /// <summary>
        /// Deprecated: Use <c>!Equals(other)</c> instead.
        /// </summary>
        public bool __Ne__(Set<K>? other) => !Equals(other);

        /// <summary>
        /// Return union with another set.
        /// </summary>
        public Set<K> Union(Set<K> other)
        {
            var result = new Set<K>();
            foreach (var key in _keys)
            {
                result.Add(key);
            }
            foreach (var item in other)
            {
                result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// Deprecated: Use <see cref="Union(Set{K})"/> instead.
        /// </summary>
        public Set<K> __Or__(Set<K> other) => Union(other);

        public static DictKeyView<K, V> operator |(DictKeyView<K, V> left, DictKeyView<K, V> right)
        {
            throw new NotSupportedException("Cannot create a DictKeyView from union operation. Use Union() to get a Set instead.");
        }

        /// <summary>
        /// Right-side union (when dict view is on the right).
        /// Deprecated: Use <see cref="Union(Set{K})"/> instead.
        /// </summary>
        public Set<K> __ROr__(Set<K> other) => Union(other);

        /// <summary>
        /// Right-side difference (when dict view is on the right: other - this).
        /// </summary>
        public Set<K> RightDifference(Set<K> other)
        {
            var result = new Set<K>();
            foreach (var item in other)
            {
                if (!Contains(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Deprecated: Use <see cref="RightDifference(Set{K})"/> instead.
        /// </summary>
        public Set<K> __RSub__(Set<K> other) => RightDifference(other);

        /// <summary>
        /// Return difference (elements in this but not in other).
        /// </summary>
        public Set<K> Difference(Set<K> other)
        {
            var result = new Set<K>();
            foreach (var key in _keys)
            {
                if (!other.Contains(key))
                {
                    result.Add(key);
                }
            }
            return result;
        }

        /// <summary>
        /// Deprecated: Use <see cref="Difference(Set{K})"/> instead.
        /// </summary>
        public Set<K> __Sub__(Set<K> other) => Difference(other);

        /// <summary>
        /// Return symmetric difference (elements in either but not both).
        /// </summary>
        public Set<K> SymmetricDifference(Set<K> other)
        {
            var result = new Set<K>();

            // Add elements from this that are not in other
            foreach (var key in _keys)
            {
                if (!other.Contains(key))
                {
                    result.Add(key);
                }
            }

            // Add elements from other that are not in this
            foreach (var item in other)
            {
                if (!Contains(item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Deprecated: Use <see cref="SymmetricDifference(Set{K})"/> instead.
        /// </summary>
        public Set<K> __XOr__(Set<K> other) => SymmetricDifference(other);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
