using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    using System.Collections;

    /// <summary>
    /// View of dictionary keys as a set-like object.
    /// This view reflects changes to the underlying dictionary.
    /// Supports set operations like intersection, union, difference.
    /// </summary>
    public sealed partial class DictKeyView<K, V>
        : IReadOnlyCollection<K>,
          ISized,
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

        /// <summary>Returns an enumerator that iterates through the keys.</summary>
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
        /// Check if this is a superset or equal to other.
        /// </summary>
        public bool IsSuperset(Set<K> other)
        {
            return Equals(other) || IsProperSuperset(other);
        }

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
        /// Return union with another key view.
        /// </summary>
        public Set<K> Union(DictKeyView<K, V> other)
        {
            var result = new Set<K>();
            foreach (var key in _keys)
            {
                result.Add(key);
            }
            foreach (var key in other)
            {
                result.Add(key);
            }
            return result;
        }

        /// <summary>
        /// Return intersection with another key view.
        /// </summary>
        public Set<K> Intersection(DictKeyView<K, V> other)
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
        /// Return difference (keys in this but not in the other view).
        /// </summary>
        public Set<K> Difference(DictKeyView<K, V> other)
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
        /// Return symmetric difference with another key view.
        /// </summary>
        public Set<K> SymmetricDifference(DictKeyView<K, V> other)
        {
            var result = new Set<K>();
            foreach (var key in _keys)
            {
                if (!other.Contains(key))
                {
                    result.Add(key);
                }
            }
            foreach (var key in other)
            {
                if (!Contains(key))
                {
                    result.Add(key);
                }
            }
            return result;
        }

        // ---- Python set algebra (PEP 3106) --------------------------------------------------
        // CPython's dict_keys is set-like: `d.keys() | e.keys()`, `&`, `-`, `^` all return a plain
        // `set`, never a view (measured, python3.12). The result type is therefore Set<K> — the
        // view stays a live window onto its dictionary and is never synthesized from an operation.
        // These delegate to the methods above so operator and method spellings cannot drift.

        /// <summary>Union: keys in either view (CPython <c>d.keys() | e.keys()</c>).</summary>
        public static Set<K> operator |(DictKeyView<K, V> left, DictKeyView<K, V> right) => left.Union(right);

        /// <summary>Union with a set.</summary>
        public static Set<K> operator |(DictKeyView<K, V> left, Set<K> right) => left.Union(right);

        /// <summary>Union with a set on the left.</summary>
        public static Set<K> operator |(Set<K> left, DictKeyView<K, V> right) => right.Union(left);

        /// <summary>Intersection: keys in both (CPython <c>d.keys() &amp; e.keys()</c>).</summary>
        public static Set<K> operator &(DictKeyView<K, V> left, DictKeyView<K, V> right) => left.Intersection(right);

        /// <summary>Intersection with a set.</summary>
        public static Set<K> operator &(DictKeyView<K, V> left, Set<K> right) => left.Intersection(right);

        /// <summary>Intersection with a set on the left.</summary>
        public static Set<K> operator &(Set<K> left, DictKeyView<K, V> right) => right.Intersection(left);

        /// <summary>Difference: keys in the left operand only (CPython <c>d.keys() - e.keys()</c>).</summary>
        public static Set<K> operator -(DictKeyView<K, V> left, DictKeyView<K, V> right) => left.Difference(right);

        /// <summary>Difference with a set.</summary>
        public static Set<K> operator -(DictKeyView<K, V> left, Set<K> right) => left.Difference(right);

        /// <summary>
        /// Difference with a set on the left: elements of <paramref name="left"/> that the view
        /// does not contain. Not commutative, so this routes to <see cref="RightDifference"/>.
        /// </summary>
        public static Set<K> operator -(Set<K> left, DictKeyView<K, V> right) => right.RightDifference(left);

        /// <summary>Symmetric difference (CPython <c>d.keys() ^ e.keys()</c>).</summary>
        public static Set<K> operator ^(DictKeyView<K, V> left, DictKeyView<K, V> right) => left.SymmetricDifference(right);

        /// <summary>Symmetric difference with a set.</summary>
        public static Set<K> operator ^(DictKeyView<K, V> left, Set<K> right) => left.SymmetricDifference(right);

        /// <summary>Symmetric difference with a set on the left.</summary>
        public static Set<K> operator ^(Set<K> left, DictKeyView<K, V> right) => right.SymmetricDifference(left);

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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
