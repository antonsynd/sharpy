using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// A dictionary that remembers the order in which items were inserted.
    /// Like Python's collections.OrderedDict.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="ISized"/> (<c>__len__</c> → <c>len(od)</c>) and
    /// <see cref="IEnumerable{T}"/> over the KEYS in insertion order (<c>__iter__</c> → <c>for k in od</c>,
    /// <c>list(od)</c>). This is the ONLY generic <c>IEnumerable</c> the type exposes so that
    /// <c>list(od)</c> binds <c>Builtins.List&lt;K&gt;(IEnumerable&lt;K&gt;)</c> unambiguously (#1933).
    /// </remarks>
    [SharpyModuleType("collections", "OrderedDict", MessageName = "collections.OrderedDict")]
    public class OrderedDict<K, V> : ISized, IEnumerable<K>, IEquatable<OrderedDict<K, V>> where K : notnull
    {
        private readonly System.Collections.Generic.List<KeyValuePair<K, V>> _items;
        private readonly System.Collections.Generic.Dictionary<K, int> _index;
        private bool _hasNullKey;
        private int _nullIndex;

        private static bool IsNullKey(K key) => key is null;

        /// <summary>Create an empty ordered dictionary.</summary>
        public OrderedDict()
        {
            _items = new System.Collections.Generic.List<KeyValuePair<K, V>>();
            _index = new System.Collections.Generic.Dictionary<K, int>();
            _nullIndex = -1;
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
                if (IsNullKey(key))
                {
                    if (!_hasNullKey)
                        throw new KeyError("None");
                    return _items[_nullIndex].Value;
                }

                if (!_index.TryGetValue(key, out int idx))
                {
                    throw new KeyError(key?.ToString() ?? "None");
                }
                return _items[idx].Value;
            }
            set
            {
                if (IsNullKey(key))
                {
                    if (_hasNullKey)
                    {
                        _items[_nullIndex] = new KeyValuePair<K, V>(key, value);
                    }
                    else
                    {
                        _nullIndex = _items.Count;
                        _hasNullKey = true;
                        _items.Add(new KeyValuePair<K, V>(key, value));
                    }
                    return;
                }

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
            if (IsNullKey(key))
                return _hasNullKey;
            return _index.ContainsKey(key);
        }

        /// <inheritdoc cref="ContainsKey"/>
        public bool Contains(K key) => ContainsKey(key);

        /// <summary>
        /// Remove the specified key and return its value.
        /// </summary>
        public V Pop(K key)
        {
            if (IsNullKey(key))
            {
                if (!_hasNullKey)
                    throw new KeyError("None");
                V val = _items[_nullIndex].Value;
                RemoveAtIndex(_nullIndex);
                return val;
            }

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
            if (IsNullKey(key))
            {
                if (!_hasNullKey)
                    return @default;
                V val = _items[_nullIndex].Value;
                RemoveAtIndex(_nullIndex);
                return val;
            }

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
            int idx;
            if (IsNullKey(key))
            {
                if (!_hasNullKey)
                    throw new KeyError("None");
                idx = _nullIndex;
            }
            else
            {
                if (!_index.TryGetValue(key, out idx))
                {
                    throw new KeyError(key?.ToString() ?? "None");
                }
            }

            var kvp = _items[idx];
            RemoveAtIndex(idx);

            if (last)
            {
                if (IsNullKey(key))
                {
                    _nullIndex = _items.Count;
                    _hasNullKey = true;
                }
                else
                {
                    _index[key] = _items.Count;
                }
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
            _hasNullKey = false;
            _nullIndex = -1;
        }

        /// <summary>
        /// Return the keys in insertion order as a sized list (a copy, matching Python's view length
        /// and order; <c>len</c>/<c>list</c>/iteration all work).
        /// </summary>
        public List<K> Keys()
        {
            return new List<K>(_items.Select(kvp => kvp.Key));
        }

        /// <summary>
        /// Return the values in insertion order as a sized list.
        /// </summary>
        public List<V> Values()
        {
            return new List<V>(_items.Select(kvp => kvp.Value));
        }

        /// <summary>
        /// Return the (key, value) pairs in insertion order as a sized list.
        /// </summary>
        public List<(K, V)> Items()
        {
            return new List<(K, V)>(_items.Select(kvp => (kvp.Key, kvp.Value)));
        }

        /// <summary>
        /// Iterate the keys in insertion order (Python's <c>__iter__</c>).
        /// </summary>
        public IEnumerator<K> GetEnumerator()
        {
            foreach (var kvp in _items)
            {
                yield return kvp.Key;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Return a shallow copy.
        /// </summary>
        public OrderedDict<K, V> Copy()
        {
            var copy = new OrderedDict<K, V>();
            foreach (var (key, value) in Items())
            {
                copy[key] = value;
            }
            return copy;
        }

        /// <summary>
        /// Get the value for a key, or a default.
        /// </summary>
        public V Get(K key, V @default = default!)
        {
            if (IsNullKey(key))
            {
                return _hasNullKey ? _items[_nullIndex].Value : @default;
            }

            if (_index.TryGetValue(key, out int idx))
            {
                return _items[idx].Value;
            }
            return @default;
        }

        /// <summary>The ordered (key, value) pairs, for equality comparison by sibling mappings.</summary>
        internal IReadOnlyList<KeyValuePair<K, V>> ItemsList => _items;

        /// <summary>
        /// Python's <c>repr(od)</c>/<c>str(od)</c>: <c>OrderedDict({...})</c> with the pairs in
        /// insertion order, or <c>OrderedDict()</c> when empty (CPython 3.12). The braces are
        /// <see cref="Dict{K, V}"/>'s own repr, so there is one spelling of the mapping rule.
        /// </summary>
        public override string ToString()
            => _items.Count == 0 ? "OrderedDict()" : "OrderedDict(" + new Dict<K, V>(_items).ToString() + ")";

        // ── Equality (Python __eq__). OrderedDict == OrderedDict is ORDER-SENSITIVE; every other
        //    pairing compares by contents. Declared as operator ==/!= so the checker discovers them
        //    through the same CLR op_Equality path Dict<K,V> uses (Dict.cs:286) — no compiler arm (#1933).

        /// <summary>Order-sensitive equality with another OrderedDict (Python semantics).</summary>
        public bool Equals(OrderedDict<K, V>? other)
            => other is not null && MappingEquality.OrderedEquals<K, V>(_items, other._items);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is OrderedDict<K, V> other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Count;

        /// <summary>OrderedDict == OrderedDict — order-sensitive.</summary>
        public static bool operator ==(OrderedDict<K, V>? left, OrderedDict<K, V>? right)
            => left is null ? right is null : left.Equals(right);

        /// <summary>OrderedDict != OrderedDict.</summary>
        public static bool operator !=(OrderedDict<K, V>? left, OrderedDict<K, V>? right) => !(left == right);

        /// <summary>OrderedDict == dict — pairwise (order-insensitive), matching CPython.</summary>
        public static bool operator ==(OrderedDict<K, V>? left, Dict<K, V>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left._items, right.Count, right);

        /// <summary>OrderedDict != dict.</summary>
        public static bool operator !=(OrderedDict<K, V>? left, Dict<K, V>? right) => !(left == right);

        /// <summary>dict == OrderedDict — pairwise, delegating to the symmetric overload.</summary>
        public static bool operator ==(Dict<K, V>? left, OrderedDict<K, V>? right) => right == left;

        /// <summary>dict != OrderedDict.</summary>
        public static bool operator !=(Dict<K, V>? left, OrderedDict<K, V>? right) => !(left == right);

        /// <summary>OrderedDict == ChainMap — pairwise cross-stdlib pair (#1933).</summary>
        public static bool operator ==(OrderedDict<K, V>? left, ChainMap<K, V>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left._items, right.Count, right.PairsForEquality());

        /// <summary>OrderedDict != ChainMap.</summary>
        public static bool operator !=(OrderedDict<K, V>? left, ChainMap<K, V>? right) => !(left == right);

        private void RemoveAtIndex(int idx)
        {
            K key = _items[idx].Key;
            _items.RemoveAt(idx);

            if (IsNullKey(key))
            {
                _hasNullKey = false;
                _nullIndex = -1;
            }
            else
            {
                _index.Remove(key);
            }

            // Rebuild indices for items after the removed one
            for (int i = idx; i < _items.Count; i++)
            {
                K k = _items[i].Key;
                if (IsNullKey(k))
                {
                    _nullIndex = i;
                }
                else
                {
                    _index[k] = i;
                }
            }
        }

        private void RebuildIndex()
        {
            _index.Clear();
            _hasNullKey = false;
            _nullIndex = -1;

            for (int i = 0; i < _items.Count; i++)
            {
                K k = _items[i].Key;
                if (IsNullKey(k))
                {
                    _hasNullKey = true;
                    _nullIndex = i;
                }
                else
                {
                    _index[k] = i;
                }
            }
        }
    }
}
