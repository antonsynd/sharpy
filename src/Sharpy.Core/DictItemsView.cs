using System.Collections.Generic;
using System;
namespace Sharpy
{
    using System.Collections;

    /// <summary>
    /// View of dictionary items as (key, value) tuples.
    /// This view reflects changes to the underlying dictionary.
    /// </summary>
    public sealed class DictItemsView<K, V>
        : IReadOnlyCollection<(K, V)>,
          ISized
        where K : notnull
    {
        private readonly Dictionary<K, V> _dict;

        internal DictItemsView(Dictionary<K, V> dict)
        {
            _dict = dict;
        }

        /// <summary>
        /// Gets the number of items in the view.
        /// </summary>
        public int Count => _dict.Count;

        /// <summary>
        /// Determines whether the view contains the specified key-value pair.
        /// </summary>
        public bool Contains((K, V) item)
        {
            if (_dict.TryGetValue(item.Item1, out V? value))
            {
                // Use Operator.Eq for proper equality comparison
                return Operator.Eq(value, item.Item2);
            }
            return false;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the items.
        /// </summary>
        public IEnumerator<(K, V)> GetEnumerator()
        {
            foreach (var kvp in _dict)
            {
                yield return (kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Return union with a set of items.
        /// </summary>
        public Set<(K, V)> Union(Set<(K, V)> other)
        {
            var result = new Set<(K, V)>();
            foreach (var item in this)
            {
                result.Add(item);
            }
            foreach (var item in other)
            {
                result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// Return union with another items view.
        /// </summary>
        public Set<(K, V)> Union(DictItemsView<K, V> other)
        {
            var result = new Set<(K, V)>();
            foreach (var item in this)
            {
                result.Add(item);
            }
            foreach (var item in other)
            {
                result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// Return intersection with a set of items.
        /// </summary>
        public Set<(K, V)> Intersection(Set<(K, V)> other)
        {
            var result = new Set<(K, V)>();
            foreach (var item in this)
            {
                if (other.Contains(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Return intersection with another items view.
        /// </summary>
        public Set<(K, V)> Intersection(DictItemsView<K, V> other)
        {
            var result = new Set<(K, V)>();
            foreach (var item in this)
            {
                if (other.Contains(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Return difference (items in this but not in <paramref name="other"/>).
        /// </summary>
        public Set<(K, V)> Difference(Set<(K, V)> other)
        {
            var result = new Set<(K, V)>();
            foreach (var item in this)
            {
                if (!other.Contains(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Return difference (items in this but not in the other view).
        /// </summary>
        public Set<(K, V)> Difference(DictItemsView<K, V> other)
        {
            var result = new Set<(K, V)>();
            foreach (var item in this)
            {
                if (!other.Contains(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Right-side difference (when the view is on the right: other - this).
        /// </summary>
        public Set<(K, V)> RightDifference(Set<(K, V)> other)
        {
            var result = new Set<(K, V)>();
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
        /// Return symmetric difference with a set of items.
        /// </summary>
        public Set<(K, V)> SymmetricDifference(Set<(K, V)> other)
        {
            var result = new Set<(K, V)>();
            foreach (var item in this)
            {
                if (!other.Contains(item))
                {
                    result.Add(item);
                }
            }
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
        /// Return symmetric difference with another items view.
        /// </summary>
        public Set<(K, V)> SymmetricDifference(DictItemsView<K, V> other)
        {
            var result = new Set<(K, V)>();
            foreach (var item in this)
            {
                if (!other.Contains(item))
                {
                    result.Add(item);
                }
            }
            foreach (var item in other)
            {
                if (!Contains(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        // ---- Python set algebra (PEP 3106) --------------------------------------------------
        // CPython's dict_items is set-like when its values are hashable: `d.items() | e.items()`,
        // `&`, `-`, `^` return a plain `set` of (key, value) tuples (measured, python3.12). Mirrors
        // DictKeyView's algebra; operators delegate to the methods so the two spellings agree.

        /// <summary>Union: items in either view (CPython <c>d.items() | e.items()</c>).</summary>
        public static Set<(K, V)> operator |(DictItemsView<K, V> left, DictItemsView<K, V> right) => left.Union(right);

        /// <summary>Union with a set.</summary>
        public static Set<(K, V)> operator |(DictItemsView<K, V> left, Set<(K, V)> right) => left.Union(right);

        /// <summary>Union with a set on the left.</summary>
        public static Set<(K, V)> operator |(Set<(K, V)> left, DictItemsView<K, V> right) => right.Union(left);

        /// <summary>Intersection: items in both (CPython <c>d.items() &amp; e.items()</c>).</summary>
        public static Set<(K, V)> operator &(DictItemsView<K, V> left, DictItemsView<K, V> right) => left.Intersection(right);

        /// <summary>Intersection with a set.</summary>
        public static Set<(K, V)> operator &(DictItemsView<K, V> left, Set<(K, V)> right) => left.Intersection(right);

        /// <summary>Intersection with a set on the left.</summary>
        public static Set<(K, V)> operator &(Set<(K, V)> left, DictItemsView<K, V> right) => right.Intersection(left);

        /// <summary>Difference: items in the left operand only (CPython <c>d.items() - e.items()</c>).</summary>
        public static Set<(K, V)> operator -(DictItemsView<K, V> left, DictItemsView<K, V> right) => left.Difference(right);

        /// <summary>Difference with a set.</summary>
        public static Set<(K, V)> operator -(DictItemsView<K, V> left, Set<(K, V)> right) => left.Difference(right);

        /// <summary>
        /// Difference with a set on the left: elements of <paramref name="left"/> that the view
        /// does not contain. Not commutative, so this routes to <see cref="RightDifference"/>.
        /// </summary>
        public static Set<(K, V)> operator -(Set<(K, V)> left, DictItemsView<K, V> right) => right.RightDifference(left);

        /// <summary>Symmetric difference (CPython <c>d.items() ^ e.items()</c>).</summary>
        public static Set<(K, V)> operator ^(DictItemsView<K, V> left, DictItemsView<K, V> right) => left.SymmetricDifference(right);

        /// <summary>Symmetric difference with a set.</summary>
        public static Set<(K, V)> operator ^(DictItemsView<K, V> left, Set<(K, V)> right) => left.SymmetricDifference(right);

        /// <summary>Symmetric difference with a set on the left.</summary>
        public static Set<(K, V)> operator ^(Set<(K, V)> left, DictItemsView<K, V> right) => right.SymmetricDifference(left);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
