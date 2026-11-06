using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    public sealed partial class Dict<K, V>
    {
        /// <summary>
        /// Allocation-free struct enumerator over the keys, in insertion order.
        /// </summary>
        public struct KeyEnumerator : IEnumerator<K>
        {
            private EntryCursor _cursor;

            internal KeyEnumerator(Dict<K, V> dict)
            {
                _cursor = dict.CreateCursor();
            }

            public K Current => _cursor.Key;

            object IEnumerator.Current => _cursor.Key!;

            public bool MoveNext() => _cursor.MoveNext();

            public void Reset() => _cursor.Reset();

            public void Dispose()
            {
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
        public int Count => _count;

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
            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                yield return cursor.Key;
            }
        }

        private IEnumerable<V> EnumerateValues() => EnumerateValuesInOrder();

        /// <summary>
        /// Determines whether the dictionary contains the specified key.
        /// </summary>
        public bool ContainsKey(K key) => FindSlot(key) >= 0;

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        public bool TryGetValue(K key, out V value)
        {
            int slot = FindSlot(key);
            if (slot >= 0)
            {
                value = _entries[slot].Value;
                return true;
            }

            value = default!;
            return false;
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
            if (FindSlot(key) >= 0)
            {
                throw new System.ArgumentException(IsNullKey(key)
                    ? "An element with the key 'None' already exists."
                    : $"An element with the key '{key}' already exists.");
            }

            InsertNew(key, value);
        }

        /// <summary>
        /// Removes the value with the specified key from the dictionary.
        /// </summary>
        /// <returns>true if the element was removed; false if key was not found.</returns>
        bool System.Collections.Generic.IDictionary<K, V>.Remove(K key)
        {
            int slot = FindSlot(key);
            if (slot < 0)
            {
                return false;
            }

            RemoveSlot(slot);
            return true;
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
            int slot = FindSlot(item.Key);
            return slot >= 0 && PairValueEquals(slot, item.Value);
        }

        /// <summary>
        /// The pair routes' value comparison. A pair under the None key compares through
        /// <c>Operator.Eq</c>; any other through the default comparer, as <c>Dictionary</c>'s
        /// <c>ICollection</c> did.
        /// </summary>
        private bool PairValueEquals(int slot, V value) => slot == _nullSlot
            ? Operator.Eq(_entries[slot].Value, value)
            : EqualityComparer<V>.Default.Equals(_entries[slot].Value, value);

        /// <summary>
        /// Copies the elements of the dictionary to an array.
        /// </summary>
        void System.Collections.Generic.ICollection<KeyValuePair<K, V>>.CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            if (array is null)
            {
                throw new System.ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            if (array.Length - arrayIndex < _count)
            {
                throw new System.ArgumentException("Destination array is not long enough to copy all the items in the collection.");
            }

            foreach (var pair in EnumeratePairs())
            {
                array[arrayIndex++] = pair;
            }
        }

        /// <summary>
        /// Removes a specific key/value pair from the dictionary.
        /// </summary>
        bool System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Remove(KeyValuePair<K, V> item)
        {
            int slot = FindSlot(item.Key);
            if (slot < 0 || !PairValueEquals(slot, item.Value))
            {
                return false;
            }

            RemoveSlot(slot);
            return true;
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
            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                yield return new KeyValuePair<K, V>(cursor.Key, cursor.Value);
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
                int slot = FindSlotOfObject(key);
                return slot >= 0 ? _entries[slot].Value : null;
            }
            set
            {
                if (key is null)
                {
                    StoreNull(ValueFromObject(value));
                    return;
                }

                this[KeyFromObject(key)] = ValueFromObject(value);
            }
        }

        private static K KeyFromObject(object key)
        {
            if (key is K typedKey)
            {
                return typedKey;
            }

            throw new System.ArgumentException($"The value \"{key}\" is not of type \"{PyFormat.PyTypeName(typeof(K))}\" and cannot be used in this generic collection.", nameof(key));
        }

        private static V ValueFromObject(object? value)
        {
            if (value is V typedValue)
            {
                return typedValue;
            }

            if (value is null)
            {
                if (default(V) is null)
                {
                    return default!;
                }

                throw new System.ArgumentNullException(nameof(value));
            }

            throw new System.ArgumentException($"The value \"{value}\" is not of type \"{PyFormat.PyTypeName(typeof(V))}\" and cannot be used in this generic collection.", nameof(value));
        }

        /// <summary>Adds an element with the provided key and value (non-generic).</summary>
        void IDictionary.Add(object key, object? value)
        {
            if (FindSlotOfObject(key) >= 0)
            {
                throw new System.ArgumentException($"An element with the key '{key ?? "None"}' already exists.");
            }

            if (key is null)
            {
                StoreNull(ValueFromObject(value));
                return;
            }

            InsertNew(KeyFromObject(key), ValueFromObject(value));
        }

        /// <summary>Determines whether the dictionary contains an element with the specified key (non-generic).</summary>
        bool IDictionary.Contains(object key) => FindSlotOfObject(key) >= 0;

        /// <summary>Returns an enumerator that iterates through the dictionary (non-generic).</summary>
        IDictionaryEnumerator IDictionary.GetEnumerator() => new EntryDictionaryEnumerator(this);

        /// <summary>Removes the element with the specified key (non-generic).</summary>
        void IDictionary.Remove(object key)
        {
            int slot = FindSlotOfObject(key);
            if (slot >= 0)
            {
                RemoveSlot(slot);
            }
        }

        /// <summary>Removes all elements from the dictionary.</summary>
        void IDictionary.Clear() => Clear();

        /// <summary>Copies the elements of the dictionary to an array.</summary>
        void ICollection.CopyTo(System.Array array, int index)
        {
            if (array is null)
            {
                throw new System.ArgumentNullException(nameof(array));
            }

            if (array.Rank != 1 || array.GetLowerBound(0) != 0)
            {
                throw new System.ArgumentException("Only single dimensional, zero-based arrays are supported.", nameof(array));
            }

            if (index < 0 || index > array.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }

            if (array.Length - index < _count)
            {
                throw new System.ArgumentException("Destination array is not long enough to copy all the items in the collection.");
            }

            switch (array)
            {
                case KeyValuePair<K, V>[] pairs:
                    foreach (var pair in EnumeratePairs())
                    {
                        pairs[index++] = pair;
                    }

                    break;
                case DictionaryEntry[] entries:
                    foreach (var pair in EnumeratePairs())
                    {
                        entries[index++] = new DictionaryEntry(pair.Key!, pair.Value);
                    }

                    break;
                case object[] objects:
                    try
                    {
                        foreach (var pair in EnumeratePairs())
                        {
                            objects[index++] = pair;
                        }
                    }
                    catch (System.ArrayTypeMismatchException)
                    {
                        throw new System.ArgumentException("Target array type is not compatible with the type of items in the collection.", nameof(array));
                    }

                    break;
                default:
                    throw new System.ArgumentException("Target array type is not compatible with the type of items in the collection.", nameof(array));
            }
        }

        private sealed class EntryDictionaryEnumerator : IDictionaryEnumerator
        {
            private EntryCursor _cursor;

            public EntryDictionaryEnumerator(Dict<K, V> dict)
            {
                _cursor = dict.CreateCursor();
            }

            public object Key => _cursor.Key!;

            public object? Value => _cursor.Value;

            public DictionaryEntry Entry => new DictionaryEntry(_cursor.Key!, _cursor.Value);

            public object Current => Entry;

            public bool MoveNext() => _cursor.MoveNext();

            public void Reset() => _cursor.Reset();
        }

        /// <summary>Gets a value indicating whether access to the dictionary is synchronized. Always false.</summary>
        bool ICollection.IsSynchronized => false;

        /// <summary>Gets an object that can be used to synchronize access to the dictionary.</summary>
        object ICollection.SyncRoot => ((ICollection)_index).SyncRoot;

        /// <summary>Gets the number of key/value pairs in the dictionary.</summary>
        int ICollection.Count => Count;

        #endregion
    }
}
