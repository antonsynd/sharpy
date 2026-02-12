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
        : IReadOnlyCollection<(K, V)>
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
        /// Deprecated: Use <see cref="Contains(ValueTuple{K, V})"/> instead.
        /// </summary>
        public bool __Contains__((K, V) item) => Contains(item);

        /// <summary>
        /// Deprecated: Use <see cref="GetEnumerator()"/> instead.
        /// </summary>
        public Iterator<(K, V)> __Iter__()
        {
            return new EnumeratorIterator<(K, V)>(GetEnumerator());
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
