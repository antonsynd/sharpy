using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    public sealed partial class Dict<K, V>
    {
        /// <summary>
        /// Returns an enumerator that iterates through the keys.
        /// </summary>
        public IEnumerator<K> GetEnumerator()
        {
            return _dict.Keys.GetEnumerator();
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
        public int Count => _dict.Count;

        /// <summary>
        /// Gets a collection containing the keys in the dictionary.
        /// </summary>
        System.Collections.Generic.ICollection<K> System.Collections.Generic.IDictionary<K, V>.Keys => _dict.Keys;

        /// <summary>
        /// Gets a collection containing the values in the dictionary.
        /// </summary>
        System.Collections.Generic.ICollection<V> System.Collections.Generic.IDictionary<K, V>.Values => _dict.Values;

        /// <summary>
        /// Gets an enumerable containing the keys in the dictionary.
        /// </summary>
        IEnumerable<K> System.Collections.Generic.IReadOnlyDictionary<K, V>.Keys => _dict.Keys;

        /// <summary>
        /// Gets an enumerable containing the values in the dictionary.
        /// </summary>
        IEnumerable<V> System.Collections.Generic.IReadOnlyDictionary<K, V>.Values => _dict.Values;

        /// <summary>
        /// Determines whether the dictionary contains the specified key.
        /// </summary>
        public bool ContainsKey(K key) => _dict.ContainsKey(key);

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        public bool TryGetValue(K key, out V value) => _dict.TryGetValue(key, out value!);

        /// <summary>
        /// For collection initializers.
        /// </summary>
        public void Add(K key, V value) => _dict[key] = value;

        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// </summary>
        /// <exception cref="System.ArgumentException">An element with the same key already exists.</exception>
        void System.Collections.Generic.IDictionary<K, V>.Add(K key, V value)
        {
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
        bool System.Collections.Generic.IDictionary<K, V>.Remove(K key) => _dict.Remove(key);

        #endregion

        #region ICollection<KeyValuePair<K,V>> implementation

        /// <summary>
        /// Adds a key/value pair to the dictionary.
        /// </summary>
        void System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Add(KeyValuePair<K, V> item)
        {
            ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).Add(item);
        }

        /// <summary>
        /// Determines whether the dictionary contains a specific key/value pair.
        /// </summary>
        bool System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Contains(KeyValuePair<K, V> item)
        {
            return ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).Contains(item);
        }

        /// <summary>
        /// Copies the elements of the dictionary to an array.
        /// </summary>
        void System.Collections.Generic.ICollection<KeyValuePair<K, V>>.CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Removes a specific key/value pair from the dictionary.
        /// </summary>
        bool System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Remove(KeyValuePair<K, V> item)
        {
            return ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).Remove(item);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the key/value pairs.
        /// </summary>
        IEnumerator<KeyValuePair<K, V>> System.Collections.Generic.IEnumerable<KeyValuePair<K, V>>.GetEnumerator()
        {
            return _dict.GetEnumerator();
        }

        #endregion

        #region System.Collections.IDictionary (non-generic)

        /// <summary>Gets a value indicating whether the dictionary has a fixed size. Always false.</summary>
        bool IDictionary.IsFixedSize => false;

        /// <summary>Gets a value indicating whether the dictionary is read-only. Always false.</summary>
        bool IDictionary.IsReadOnly => false;

        /// <summary>Gets a collection containing the keys (non-generic).</summary>
        ICollection IDictionary.Keys => ((IDictionary)_dict).Keys;

        /// <summary>Gets a collection containing the values (non-generic).</summary>
        ICollection IDictionary.Values => ((IDictionary)_dict).Values;

        /// <summary>Gets or sets the value associated with the specified key (non-generic).</summary>
        object? IDictionary.this[object key]
        {
            get => ((IDictionary)_dict)[key];
            set => ((IDictionary)_dict)[key] = value;
        }

        /// <summary>Adds an element with the provided key and value (non-generic).</summary>
        void IDictionary.Add(object key, object? value) => ((IDictionary)_dict).Add(key, value);

        /// <summary>Determines whether the dictionary contains an element with the specified key (non-generic).</summary>
        bool IDictionary.Contains(object key) => ((IDictionary)_dict).Contains(key);

        /// <summary>Returns an enumerator that iterates through the dictionary (non-generic).</summary>
        IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)_dict).GetEnumerator();

        /// <summary>Removes the element with the specified key (non-generic).</summary>
        void IDictionary.Remove(object key) => ((IDictionary)_dict).Remove(key);

        /// <summary>Removes all elements from the dictionary.</summary>
        void IDictionary.Clear() => _dict.Clear();

        /// <summary>Copies the elements of the dictionary to an array.</summary>
        void ICollection.CopyTo(System.Array array, int index) => ((ICollection)_dict).CopyTo(array, index);

        /// <summary>Gets a value indicating whether access to the dictionary is synchronized. Always false.</summary>
        bool ICollection.IsSynchronized => false;

        /// <summary>Gets an object that can be used to synchronize access to the dictionary.</summary>
        object ICollection.SyncRoot => ((ICollection)_dict).SyncRoot;

        /// <summary>Gets the number of key/value pairs in the dictionary.</summary>
        int ICollection.Count => _dict.Count;

        #endregion
    }
}
