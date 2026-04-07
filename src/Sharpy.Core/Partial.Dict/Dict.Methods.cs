using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    using static Builtins;

    /// <summary>
    /// A mutable mapping of keys to values, similar to Python's dict.
    /// Supports Python-style methods like get(), pop(), items(), keys(), and values().
    /// </summary>
    public sealed partial class Dict<K, V>
    {
        /// <summary>
        /// Return a shallow copy of the dictionary.
        /// </summary>
        /// <returns>A new dictionary with the same key-value pairs.</returns>
        /// <example>
        /// <code>
        /// d = {"a": 1, "b": 2}
        /// e = d.copy()    # {"a": 1, "b": 2}
        /// </code>
        /// </example>
        public Dict<K, V> Copy()
        {
            var newDict = new Dict<K, V>();

            foreach (var kv in _dict)
            {
                newDict._dict[kv.Key] = kv.Value;
            }

            return newDict;
        }

        /// <inheritdoc/>
        object IShallowCopyable.ShallowCopy()
        {
            return Copy();
        }

        /// <summary>
        /// Remove all items from the dictionary.
        /// </summary>
        /// <example>
        /// <code>
        /// d = {"a": 1, "b": 2}
        /// d.clear()    # {}
        /// </code>
        /// </example>
        public void Clear()
        {
            _dict.Clear();
        }

        /// <summary>
        /// Check if <paramref name="key"/> exists in the dictionary.
        /// Used by the compiler for <c>key in dict</c> expressions.
        /// </summary>
        public bool Contains(K key) => ContainsKey(key);

        /// <summary>
        /// Return the value for <paramref name="key"/> if present, otherwise
        /// <see cref="Optional{T}.None"/>.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>An <see cref="Optional{T}"/> containing the value, or None.</returns>
        /// <example>
        /// <code>
        /// d = {"a": 1}
        /// d.get("a")    # Some(1)
        /// d.get("z")    # None
        /// </code>
        /// </example>
        public Optional<V> Get(K key)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                return Optional<V>.Some(value!);
            }

            return Optional<V>.None;
        }

        /// <summary>
        /// Return the value for <paramref name="key"/> if present, otherwise
        /// <paramref name="default"/>.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="default">The fallback value.</param>
        /// <returns>The value for the key, or the default.</returns>
        /// <example>
        /// <code>
        /// d = {"a": 1}
        /// d.get("a", 0)    # 1
        /// d.get("z", 0)    # 0
        /// </code>
        /// </example>
        public V Get(K key, V @default)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                return value;
            }

            return @default;
        }

        /// <summary>
        /// Return a view of the dictionary's key-value pairs.
        /// </summary>
        /// <returns>A view of <c>(key, value)</c> pairs.</returns>
        /// <example>
        /// <code>
        /// d = {"a": 1, "b": 2}
        /// for k, v in d.items():
        ///     print(k, v)
        /// </code>
        /// </example>
        public DictItemsView<K, V> Items()
        {
            return new DictItemsView<K, V>(_dict);
        }

        /// <summary>
        /// Return a view of the dictionary's keys.
        /// </summary>
        /// <returns>A view of the keys.</returns>
        /// <example>
        /// <code>
        /// d = {"a": 1, "b": 2}
        /// d.keys()    # ["a", "b"]
        /// </code>
        /// </example>
        public DictKeyView<K, V> Keys()
        {
            return new DictKeyView<K, V>(_dict.Keys);
        }

        /// <summary>
        /// Remove the specified key and return the corresponding value.
        /// Raises <see cref="KeyError"/> if the key is not found.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>The value that was associated with the key.</returns>
        /// <exception cref="KeyError">Thrown if the key is not found.</exception>
        /// <example>
        /// <code>
        /// d = {"a": 1, "b": 2}
        /// d.pop("a")    # 1, d is {"b": 2}
        /// </code>
        /// </example>
        public V Pop(K key)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                _dict.Remove(key);
                return value;
            }

            throw new KeyError(Repr(key));
        }

        /// <summary>
        /// Remove the specified key and return the corresponding value.
        /// If the key is not found, return <paramref name="default"/>.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <param name="default">Value to return if key is not found.</param>
        /// <returns>The removed value or the default.</returns>
        /// <example>
        /// <code>
        /// d = {"a": 1}
        /// d.pop("z", 0)    # 0
        /// </code>
        /// </example>
        public V Pop(K key, V @default)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                _dict.Remove(key);
                return value;
            }

            return @default;
        }

        /// <summary>
        /// Remove and return a <c>(key, value)</c> pair from the dictionary.
        /// </summary>
        /// <param name="last">If <c>true</c>, remove the last pair; otherwise the first.</param>
        /// <returns>A tuple of the removed key and value.</returns>
        /// <example>
        /// <code>
        /// d = {"a": 1, "b": 2}
        /// d.popitem()    # ("b", 2)
        /// </code>
        /// </example>
        public (K, V) PopItem(bool last = false)
        {
            var pair = last ? _dict.Last() : _dict.First();
            _dict.Remove(pair.Key);

            return (pair.Key, pair.Value);
        }

        /// <summary>
        /// If <paramref name="key"/> is in the dictionary, return its value.
        /// If not, insert <paramref name="key"/> with <paramref name="default"/>
        /// and return <paramref name="default"/>.
        /// </summary>
        /// <param name="key">The key to look up or insert.</param>
        /// <param name="default">The value to insert if key is absent.</param>
        /// <returns>The existing or newly inserted value.</returns>
        /// <example>
        /// <code>
        /// d = {"a": 1}
        /// d.setdefault("a", 0)    # 1
        /// d.setdefault("b", 0)    # 0, d is {"a": 1, "b": 0}
        /// </code>
        /// </example>
        public V SetDefault(K key, V @default)
        {
            if (_dict.TryGetValue(key, out V? value))
            {
                return value;
            }

            return _dict[key] = @default;
        }

        /// <summary>
        /// Update the dictionary with key-value pairs from <paramref name="other"/>,
        /// overwriting existing keys.
        /// </summary>
        /// <param name="other">A dictionary whose pairs are merged in.</param>
        /// <example>
        /// <code>
        /// d = {"a": 1}
        /// d.update({"a": 9, "b": 2})    # {"a": 9, "b": 2}
        /// </code>
        /// </example>
        public void Update(IReadOnlyDictionary<K, V> other)
        {
            if (other == null)
            {
                throw new System.ArgumentNullException(nameof(other));
            }

            foreach (var kvp in other)
            {
                _dict[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Update the dictionary with key-value pairs from an iterable of tuples.
        /// </summary>
        /// <param name="other">An iterable of <c>(key, value)</c> tuples.</param>
        public void Update(IEnumerable<(K, V)> other)
        {
            foreach (var (key, value) in other)
            {
                _dict[key] = value;
            }
        }

        /// <summary>
        /// Return a view of the dictionary's values.
        /// </summary>
        /// <returns>A view of the values.</returns>
        /// <example>
        /// <code>
        /// d = {"a": 1, "b": 2}
        /// d.values()    # [1, 2]
        /// </code>
        /// </example>
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

        /// <summary>Convert to a standard .NET Dictionary.</summary>
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
    }
}
