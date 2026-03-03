using System.Collections.Generic;
using System.Linq;
using System;
namespace Sharpy
{
    using System.Collections;
    using System.Text;

    using static Builtins;

    public sealed partial class Dict<K, V>
        : IDictionary<K, V>,
          IReadOnlyDictionary<K, V>,
          System.IEquatable<Dict<K, V>>,
          ISized
        where K : notnull
    {
        private readonly Dictionary<K, V> _dict;

        public Dict()
        {
            _dict = new Dictionary<K, V>();
        }

        public Dict(IReadOnlyDictionary<K, V> mapping) : this()
        {
            if (mapping is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            foreach (var kvp in mapping)
            {
                _dict[kvp.Key] = kvp.Value;
            }
        }

        public Dict(IEnumerable<(K, V)> iterable) : this()
        {
            if (iterable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            foreach (var (key, value) in iterable)
            {
                _dict[key] = value;
            }
        }

        /// <summary>
        /// Implicit conversion from Dictionary to Dict.
        /// </summary>
        public static implicit operator Dict<K, V>(Dictionary<K, V> dictionary)
        {
            var dict = new Dict<K, V>();
            foreach (var kvp in dictionary)
            {
                dict._dict[kvp.Key] = kvp.Value;
            }
            return dict;
        }

        /// <summary>
        /// Return a shallow copy of the dictionary.
        /// </summary>
        public Dict<K, V> Copy()
        {
            var newDict = new Dict<K, V>();

            foreach (var kv in _dict)
            {
                newDict._dict[kv.Key] = kv.Value;
            }

            return newDict;
        }

        public void Clear()
        {
            _dict.Clear();
        }

        /// <summary>
        /// Deprecated: Use <see cref="ContainsKey(K)"/> instead.
        /// </summary>
        public bool Contains(K key) => ContainsKey(key);

        public Optional<V> Get(K key)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                return Optional<V>.Some(value!);
            }

            return Optional<V>.None;
        }

        public V Get(K key, V @default)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                return value;
            }

            return @default;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the keys.
        /// </summary>
        public IEnumerator<K> GetEnumerator()
        {
            return _dict.Keys.GetEnumerator();
        }

        public DictItemsView<K, V> Items()
        {
            return new DictItemsView<K, V>(_dict);
        }

        public DictKeyView<K, V> Keys()
        {
            return new DictKeyView<K, V>(_dict.Keys);
        }

        public V Pop(K key)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                _dict.Remove(key);
                return value;
            }

            throw new KeyError(Repr(key));
        }

        public V Pop(K key, V @default)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                _dict.Remove(key);
                return value;
            }

            return @default;
        }

        public (K, V) PopItem(bool last = false)
        {
            var pair = last ? _dict.Last() : _dict.First();
            _dict.Remove(pair.Key);

            return (pair.Key, pair.Value);
        }

        public V SetDefault(K key, V @default)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                return value;
            }

            return _dict[key] = @default;
        }

        public void Update(IReadOnlyDictionary<K, V> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            foreach (var kvp in other)
            {
                _dict[kvp.Key] = kvp.Value;
            }
        }

        public void Update(IEnumerable<(K, V)> other)
        {
            foreach (var (key, value) in other)
            {
                _dict[key] = value;
            }
        }

        public DictValuesView<K, V> Values()
        {
            return new DictValuesView<K, V>(_dict.Values);
        }

        /// <summary>
        /// Removes the item with the specified key from the dictionary.
        /// </summary>
        /// <exception cref="KeyError">Thrown if the key does not exist.</exception>
        public void Remove(K key)
        {
            if (!_dict.Remove(key))
            {
                throw new KeyError(Repr(key));
            }
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <exception cref="KeyError">Thrown if the key does not exist on get.</exception>
        public V this[K key]
        {
            get
            {
                if (_dict.TryGetValue(key, out V? value))
                {
                    return value;
                }

                throw new KeyError(Repr(key));
            }
            set => _dict[key] = value;
        }

        /// <summary>
        /// Determines whether this dictionary equals another dictionary by comparing key-value pairs.
        /// </summary>
        public bool Equals(Dict<K, V>? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_dict.Count != other._dict.Count)
            {
                return false;
            }

            foreach (var kv in _dict)
            {
                if (!other._dict.TryGetValue(kv.Key, out V? value))
                {
                    return false;
                }

                if (!Operator.Eq(kv.Value, value))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether this dictionary is equal to the specified object.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is Dict<K, V> dict)
            {
                return Equals(dict);
            }

            return false;
        }

        /// <summary>
        /// Delegate to specialized GetEnumerator() for generalized one.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Dictionary<K, V> ToDictionary()
        {
            return new Dictionary<K, V>(_dict);
        }

        /// <summary>
        /// Returns a new dictionary that is the result of merging this dictionary with other.
        /// Keys from other take precedence.
        /// </summary>
        public Dict<K, V> Merge(Dict<K, V> other)
        {
            var newDict = Copy();
            newDict.Update(other);

            return newDict;
        }

        public static Dict<K, V> operator |(Dict<K, V> left, Dict<K, V> right)
        {
            return left.Merge(right);
        }

        public static bool operator ==(Dict<K, V>? left, Dict<K, V>? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public static bool operator !=(Dict<K, V> left, Dict<K, V> right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Returns a hash code for this dictionary.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + typeof(Dict<K, V>).GetHashCode();
                hash = hash * 31 + _dict.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Returns a string representation of this dictionary.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append('{');

            int i = 1;
            var numItems = _dict.Count;

            foreach (var kv in _dict)
            {
                builder.Append($"{Repr(kv.Key)}: {Repr(kv.Value)}");

                if (i < numItems)
                {
                    builder.Append(", ");
                }

                ++i;
            }

            builder.Append('}');

            return builder.ToString();
        }

        public static bool operator true(Dict<K, V>? dict)
        {
            return dict is not null && dict._dict.Count > 0;
        }

        public static bool operator false(Dict<K, V>? dict)
        {
            return dict is null || dict._dict.Count == 0;
        }

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
        /// <exception cref="ArgumentException">An element with the same key already exists.</exception>
        void System.Collections.Generic.IDictionary<K, V>.Add(K key, V value)
        {
            if (_dict.ContainsKey(key))
            {
                throw new ArgumentException($"An element with the key '{key}' already exists.");
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
    }
}
