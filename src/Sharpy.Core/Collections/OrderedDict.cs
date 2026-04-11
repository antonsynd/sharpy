using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// A dictionary that remembers the order in which items were inserted.
    /// Like Python's collections.OrderedDict.
    /// </summary>
    [SharpyModuleType("collections", "OrderedDict")]
    public class OrderedDict<K, V> where K : notnull
    {
        private readonly System.Collections.Generic.List<KeyValuePair<K, V>> _items;
        private readonly System.Collections.Generic.Dictionary<K, int> _index;

        /// <summary>Create an empty ordered dictionary.</summary>
        public OrderedDict()
        {
            _items = new System.Collections.Generic.List<KeyValuePair<K, V>>();
            _index = new System.Collections.Generic.Dictionary<K, int>();
        }

        /// <summary>Create an ordered dictionary from key-value pairs.</summary>
        public OrderedDict(IEnumerable<KeyValuePair<K, V>> items) : this()
        {
            foreach (var kvp in items)
            {
                this[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>Create an ordered dictionary from tuples.</summary>
        public OrderedDict(IEnumerable<(K, V)> items) : this()
        {
            foreach (var (key, value) in items)
            {
                this[key] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        public V this[K key]
        {
            get
            {
                if (!_index.TryGetValue(key, out int idx))
                {
                    throw new KeyError(key?.ToString() ?? "None");
                }
                return _items[idx].Value;
            }
            set
            {
                if (_index.TryGetValue(key, out int idx))
                {
                    _items[idx] = new KeyValuePair<K, V>(key, value);
                }
                else
                {
                    _index[key] = _items.Count;
                    _items.Add(new KeyValuePair<K, V>(key, value));
                }
            }
        }

        /// <summary>
        /// Gets the number of key/value pairs.
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Check if the dictionary contains the given key.
        /// </summary>
        public bool ContainsKey(K key)
        {
            return _index.ContainsKey(key);
        }

        /// <summary>
        /// Remove the specified key and return its value.
        /// </summary>
        public V Pop(K key)
        {
            if (!_index.TryGetValue(key, out int idx))
            {
                throw new KeyError(key?.ToString() ?? "None");
            }

            V value = _items[idx].Value;
            RemoveAtIndex(idx);
            return value;
        }

        /// <summary>
        /// Remove the specified key and return its value, or return default if not found.
        /// </summary>
        public V Pop(K key, V @default)
        {
            if (!_index.TryGetValue(key, out int idx))
            {
                return @default;
            }

            V value = _items[idx].Value;
            RemoveAtIndex(idx);
            return value;
        }

        /// <summary>
        /// Remove and return a (key, value) pair. If last is true, pairs are returned in LIFO order;
        /// if false, in FIFO order.
        /// </summary>
        public (K, V) Popitem(bool last = true)
        {
            if (_items.Count == 0)
            {
                throw new KeyError("dictionary is empty");
            }

            int idx = last ? _items.Count - 1 : 0;
            var kvp = _items[idx];
            RemoveAtIndex(idx);
            return (kvp.Key, kvp.Value);
        }

        /// <summary>
        /// Move an existing key to either end of an ordered dictionary.
        /// If last is true, move to the end; if false, move to the beginning.
        /// </summary>
        public void MoveToEnd(K key, bool last = true)
        {
            if (!_index.TryGetValue(key, out int idx))
            {
                throw new KeyError(key?.ToString() ?? "None");
            }

            var kvp = _items[idx];
            RemoveAtIndex(idx);

            if (last)
            {
                _index[key] = _items.Count;
                _items.Add(kvp);
            }
            else
            {
                _items.Insert(0, kvp);
                RebuildIndex();
            }
        }

        /// <summary>
        /// Remove all items from the dictionary.
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            _index.Clear();
        }

        /// <summary>
        /// Return the keys in insertion order.
        /// </summary>
        public IEnumerable<K> Keys()
        {
            return _items.Select(kvp => kvp.Key);
        }

        /// <summary>
        /// Return the values in insertion order.
        /// </summary>
        public IEnumerable<V> Values()
        {
            return _items.Select(kvp => kvp.Value);
        }

        /// <summary>
        /// Return the (key, value) pairs in insertion order.
        /// </summary>
        public IEnumerable<(K, V)> Items()
        {
            return _items.Select(kvp => (kvp.Key, kvp.Value));
        }

        /// <summary>
        /// Return a shallow copy.
        /// </summary>
        public OrderedDict<K, V> Copy()
        {
            var copy = new OrderedDict<K, V>();
            foreach (var kvp in _items)
            {
                copy[kvp.Key] = kvp.Value;
            }
            return copy;
        }

        /// <summary>
        /// Get the value for a key, or a default.
        /// </summary>
        public V Get(K key, V @default = default!)
        {
            if (_index.TryGetValue(key, out int idx))
            {
                return _items[idx].Value;
            }
            return @default;
        }

        private void RemoveAtIndex(int idx)
        {
            K key = _items[idx].Key;
            _items.RemoveAt(idx);
            _index.Remove(key);

            // Rebuild indices for items after the removed one
            for (int i = idx; i < _items.Count; i++)
            {
                _index[_items[i].Key] = i;
            }
        }

        private void RebuildIndex()
        {
            _index.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                _index[_items[i].Key] = i;
            }
        }
    }
}
