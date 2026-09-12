using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    public sealed partial class Dict<K, V>
    {
        /// <summary>
        /// Allocation-free struct enumerator over the keys. Splices the null-key
        /// slot at its ordinal position within the dictionary's key sequence.
        /// </summary>
        public struct KeyEnumerator : IEnumerator<K>
        {
            private Dictionary<K, V>.KeyCollection.Enumerator _inner;
            private readonly bool _hasNull;
            private readonly int _nullOrdinal;
            private int _index;
            private bool _yieldedNull;
            private K _current;

            internal KeyEnumerator(Dict<K, V> dict)
            {
                _inner = dict._dict.Keys.GetEnumerator();
                _hasNull = dict._hasNullKey;
                _nullOrdinal = dict._nullOrdinal;
                _index = -1;
                _yieldedNull = false;
                _current = default!;
            }

            public K Current => _current;

            object IEnumerator.Current => _current!;

            public bool MoveNext()
            {
                _index++;

                if (_hasNull && !_yieldedNull && _index == _nullOrdinal)
                {
                    _current = default!;
                    _yieldedNull = true;
                    return true;
                }

                if (_inner.MoveNext())
                {
                    _current = _inner.Current;
                    return true;
                }

                if (_hasNull && !_yieldedNull)
                {
                    _current = default!;
                    _yieldedNull = true;
                    return true;
                }

                return false;
            }

            public void Reset()
            {
                ((IEnumerator)_inner).Reset();
                _index = -1;
                _yieldedNull = false;
                _current = default!;
            }

            public void Dispose()
            {
                _inner.Dispose();
            }
        }

        /// <summary>
        /// Returns an allocation-free struct enumerator over the keys. Used by
        /// <c>foreach</c> on the concrete dictionary (Python iterates keys).
        /// </summary>
        public KeyEnumerator GetEnumerator()
        {
            return new KeyEnumerator(this);
        }

        /// <summary>
        /// Delegate to specialized GetEnumerator() for generalized one.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Gets a value indicating whether the dictionary is read-only. Always false.</summary>
        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        #region IDictionary<K,V> and IReadOnlyDictionary<K,V> implementation

        /// <summary>
        /// Gets the number of key/value pairs in the dictionary.
        /// </summary>
        public int Count => _dict.Count + (_hasNullKey ? 1 : 0);

        /// <summary>
        /// Gets a collection containing the keys in the dictionary.
        /// </summary>
        System.Collections.Generic.ICollection<K> System.Collections.Generic.IDictionary<K, V>.Keys => new System.Collections.Generic.List<K>(EnumerateKeys());

        /// <summary>
        /// Gets a collection containing the values in the dictionary.
        /// </summary>
        System.Collections.Generic.ICollection<V> System.Collections.Generic.IDictionary<K, V>.Values => new System.Collections.Generic.List<V>(EnumerateValues());

        /// <summary>
        /// Gets an enumerable containing the keys in the dictionary.
        /// </summary>
        IEnumerable<K> System.Collections.Generic.IReadOnlyDictionary<K, V>.Keys => EnumerateKeys();

        /// <summary>
        /// Gets an enumerable containing the values in the dictionary.
        /// </summary>
        IEnumerable<V> System.Collections.Generic.IReadOnlyDictionary<K, V>.Values => EnumerateValues();

        private IEnumerable<K> EnumerateKeys()
        {
            int dictIndex = 0;
            foreach (var key in _dict.Keys)
            {
                if (_hasNullKey && dictIndex == _nullOrdinal)
                {
                    yield return default!;
                }

                yield return key;
                dictIndex++;
            }

            if (_hasNullKey && _nullOrdinal >= _dict.Count)
            {
                yield return default!;
            }
        }

        private IEnumerable<V> EnumerateValues()
        {
            int dictIndex = 0;
            foreach (var kvp in _dict)
            {
                if (_hasNullKey && dictIndex == _nullOrdinal)
                {
                    yield return _nullValue;
                }

                yield return kvp.Value;
                dictIndex++;
            }

            if (_hasNullKey && _nullOrdinal >= _dict.Count)
            {
                yield return _nullValue;
            }
        }

        /// <summary>
        /// Determines whether the dictionary contains the specified key.
        /// </summary>
        public bool ContainsKey(K key) => IsNullKey(key) ? _hasNullKey : _dict.ContainsKey(key);

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        public bool TryGetValue(K key, out V value)
        {
            if (IsNullKey(key))
            {
                if (_hasNullKey)
                {
                    value = _nullValue;
                    return true;
                }

                value = default!;
                return false;
            }

            return _dict.TryGetValue(key, out value!);
        }

        /// <summary>
        /// For collection initializers.
        /// </summary>
        public void Add(K key, V value)
        {
            this[key] = value;
        }

        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// </summary>
        /// <exception cref="System.ArgumentException">An element with the same key already exists.</exception>
        void System.Collections.Generic.IDictionary<K, V>.Add(K key, V value)
        {
            if (IsNullKey(key))
            {
                if (_hasNullKey)
                {
                    throw new System.ArgumentException("An element with the key 'None' already exists.");
                }

                _nullOrdinal = _dict.Count;
                _nullValue = value;
                _hasNullKey = true;
                return;
            }

            if (_dict.ContainsKey(key))
            {
                throw new System.ArgumentException($"An element with the key '{key}' already exists.");
            }
            _dict.Add(key, value);
        }

        /// <summary>
        /// Removes the value with the specified key from the dictionary.
        /// </summary>
        /// <returns>true if the element was removed; false if key was not found.</returns>
        bool System.Collections.Generic.IDictionary<K, V>.Remove(K key)
        {
            if (IsNullKey(key))
            {
                if (!_hasNullKey) return false;
                _hasNullKey = false;
                _nullValue = default!;
                return true;
            }

            return _dict.Remove(key);
        }

        #endregion

        #region ICollection<KeyValuePair<K,V>> implementation

        /// <summary>
        /// Adds a key/value pair to the dictionary.
        /// </summary>
        void System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Add(KeyValuePair<K, V> item)
        {
            this[item.Key] = item.Value;
        }

        /// <summary>
        /// Determines whether the dictionary contains a specific key/value pair.
        /// </summary>
        bool System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Contains(KeyValuePair<K, V> item)
        {
            if (IsNullKey(item.Key))
            {
                return _hasNullKey && Operator.Eq(_nullValue, item.Value);
            }

            return ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).Contains(item);
        }

        /// <summary>
        /// Copies the elements of the dictionary to an array.
        /// </summary>
        void System.Collections.Generic.ICollection<KeyValuePair<K, V>>.CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).CopyTo(array, arrayIndex);
            if (_hasNullKey)
            {
                array[arrayIndex + _dict.Count] = new KeyValuePair<K, V>(default!, _nullValue);
            }
        }

        /// <summary>
        /// Removes a specific key/value pair from the dictionary.
        /// </summary>
        bool System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Remove(KeyValuePair<K, V> item)
        {
            if (IsNullKey(item.Key))
            {
                if (_hasNullKey && Operator.Eq(_nullValue, item.Value))
                {
                    _hasNullKey = false;
                    _nullValue = default!;
                    return true;
                }

                return false;
            }

            return ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).Remove(item);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the key/value pairs.
        /// </summary>
        IEnumerator<KeyValuePair<K, V>> System.Collections.Generic.IEnumerable<KeyValuePair<K, V>>.GetEnumerator()
        {
            return EnumeratePairs().GetEnumerator();
        }

        private IEnumerable<KeyValuePair<K, V>> EnumeratePairs()
        {
            int dictIndex = 0;
            foreach (var kvp in _dict)
            {
                if (_hasNullKey && dictIndex == _nullOrdinal)
                {
                    yield return new KeyValuePair<K, V>(default!, _nullValue);
                }

                yield return kvp;
                dictIndex++;
            }

            if (_hasNullKey && _nullOrdinal >= _dict.Count)
            {
                yield return new KeyValuePair<K, V>(default!, _nullValue);
            }
        }

        #endregion

        #region System.Collections.IDictionary (non-generic)

        /// <summary>Gets a value indicating whether the dictionary has a fixed size. Always false.</summary>
        bool IDictionary.IsFixedSize => false;

        /// <summary>Gets a value indicating whether the dictionary is read-only. Always false.</summary>
        bool IDictionary.IsReadOnly => false;

        /// <summary>Gets a collection containing the keys (non-generic).</summary>
        ICollection IDictionary.Keys => new System.Collections.Generic.List<K>(EnumerateKeys());

        /// <summary>Gets a collection containing the values (non-generic).</summary>
        ICollection IDictionary.Values => new System.Collections.Generic.List<V>(EnumerateValues());

        /// <summary>Gets or sets the value associated with the specified key (non-generic).</summary>
        object? IDictionary.this[object key]
        {
            get
            {
                if (key is null)
                {
                    if (_hasNullKey) return _nullValue;
                    return null;
                }

                return ((IDictionary)_dict)[key];
            }
            set
            {
                if (key is null)
                {
                    if (!_hasNullKey) _nullOrdinal = _dict.Count;
                    _nullValue = (V)value!;
                    _hasNullKey = true;
                    return;
                }

                ((IDictionary)_dict)[key] = value;
            }
        }

        /// <summary>Adds an element with the provided key and value (non-generic).</summary>
        void IDictionary.Add(object key, object? value)
        {
            if (key is null)
            {
                if (_hasNullKey)
                {
                    throw new System.ArgumentException("An element with the key 'None' already exists.");
                }

                _nullOrdinal = _dict.Count;
                _nullValue = (V)value!;
                _hasNullKey = true;
                return;
            }

            ((IDictionary)_dict).Add(key, value);
        }

        /// <summary>Determines whether the dictionary contains an element with the specified key (non-generic).</summary>
        bool IDictionary.Contains(object key)
        {
            if (key is null) return _hasNullKey;
            return ((IDictionary)_dict).Contains(key);
        }

        /// <summary>Returns an enumerator that iterates through the dictionary (non-generic).</summary>
        IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)_dict).GetEnumerator();

        /// <summary>Removes the element with the specified key (non-generic).</summary>
        void IDictionary.Remove(object key)
        {
            if (key is null)
            {
                _hasNullKey = false;
                _nullValue = default!;
                return;
            }

            ((IDictionary)_dict).Remove(key);
        }

        /// <summary>Removes all elements from the dictionary.</summary>
        void IDictionary.Clear() => Clear();

        /// <summary>Copies the elements of the dictionary to an array.</summary>
        void ICollection.CopyTo(System.Array array, int index) => ((ICollection)_dict).CopyTo(array, index);

        /// <summary>Gets a value indicating whether access to the dictionary is synchronized. Always false.</summary>
        bool ICollection.IsSynchronized => false;

        /// <summary>Gets an object that can be used to synchronize access to the dictionary.</summary>
        object ICollection.SyncRoot => ((ICollection)_dict).SyncRoot;

        /// <summary>Gets the number of key/value pairs in the dictionary.</summary>
        int ICollection.Count => Count;

        #endregion
    }
}
