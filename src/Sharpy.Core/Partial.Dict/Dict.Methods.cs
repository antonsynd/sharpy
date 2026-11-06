using System;
using System.Collections.Generic;

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
        /// Create a new dictionary with keys from <paramref name="keys"/> and values set to <paramref name="value"/>.
        /// </summary>
        /// <param name="keys">An iterable of keys for the new dictionary.</param>
        /// <param name="value">The value for all keys.</param>
        /// <returns>A new dictionary with the specified keys and value.</returns>
        /// <example>
        /// <code>
        /// d = dict.fromkeys(["a", "b"], 0)    # {"a": 0, "b": 0}
        /// </code>
        /// </example>
        // `value` is required. Python's valueless dict.fromkeys(keys) fills None, which a value-type V
        // cannot hold, so that form is the one-argument overload below (values typed object, all
        // None). A defaulted V here — `V value = default!`, or `V? value = default`, which is the same
        // parameter for a value-type V — filled 0/False where python fills None (#2054).
        public static Dict<K, V> Fromkeys(IEnumerable<K> keys, V value)
        {
            var result = new Dict<K, V>();

            foreach (var key in keys)
            {
                result[key] = value;
            }

            return result;
        }

        /// <summary>
        /// Create a new dictionary with keys from <paramref name="keys"/>, every value None —
        /// python's valueless <c>dict.fromkeys(keys)</c>. The values are typed <c>object</c>, since
        /// None is not a value of every V.
        /// </summary>
        /// <param name="keys">An iterable of keys for the new dictionary.</param>
        /// <returns>A new dictionary with the specified keys, all mapped to None.</returns>
        /// <example>
        /// <code>
        /// d = dict.fromkeys(["a", "b"])    # {"a": None, "b": None}
        /// </code>
        /// </example>
        public static Dict<K, object> Fromkeys(IEnumerable<K> keys) => Dict<K, object>.Fromkeys(keys, null!);

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
            var newDict = new Dict<K, V>(_count);
            newDict.StoreAllFrom(this);

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
            _index.Clear();
            Array.Clear(_entries, 0, _used);
            _used = 0;
            _first = 0;
            _count = 0;
            _nullSlot = -1;
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
            int slot = FindSlot(key);
            return slot >= 0 ? Optional<V>.Some(_entries[slot].Value!) : Optional<V>.None;
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
            int slot = FindSlot(key);
            return slot >= 0 ? _entries[slot].Value : @default;
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
            return new DictItemsView<K, V>(this);
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
            return new DictKeyView<K, V>(this);
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
            int slot = FindSlot(key);
            if (slot < 0)
            {
                throw new KeyError(key);
            }

            return RemoveSlot(slot);
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
            int slot = FindSlot(key);
            return slot >= 0 ? RemoveSlot(slot) : @default;
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
        public (K, V) PopItem(bool last = true)
        {
            if (Count == 0)
            {
                throw new KeyError("popitem(): dictionary is empty");
            }

            int slot = last ? _used - 1 : _first;
            K key = _entries[slot].Key;
            V value = RemoveSlot(slot);

            return (key, value);
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
            int slot = FindSlot(key);
            if (slot >= 0)
            {
                return _entries[slot].Value;
            }

            InsertNew(key, @default);
            return @default;
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
                this[kvp.Key] = kvp.Value;
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
                this[key] = value;
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
            return new DictValuesView<K, V>(this);
        }

        /// <summary>
        /// Removes the item with the specified key from the dictionary.
        /// </summary>
        /// <exception cref="KeyError">Thrown if the key does not exist.</exception>
        public void Remove(K key)
        {
            int slot = FindSlot(key);
            if (slot < 0)
            {
                throw new KeyError(key);
            }

            RemoveSlot(slot);
        }

        /// <summary>Convert to a standard .NET Dictionary.</summary>
        public Dictionary<K, V> ToDictionary()
        {
            if (_nullSlot >= 0)
            {
                throw new TypeError("Cannot convert to Dictionary<K,V>: contains a None key that Dictionary does not support");
            }

            var result = new Dictionary<K, V>(_count);
            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                result.Add(cursor.Key, cursor.Value);
            }

            return result;
        }

        /// <summary>
        /// Returns a new dictionary that is the result of merging this dictionary with other.
        /// Keys from other take precedence.
        /// </summary>
        public void Update(Dict<K, V> other)
        {
            if (other == null)
            {
                throw new System.ArgumentNullException(nameof(other));
            }

            StoreAllFrom(other);
        }

        /// <summary>
        /// Stores every entry of <paramref name="other"/>, in its order. Self-update is safe: storing
        /// onto existing keys adds none, so the cursor stays valid.
        /// </summary>
        private void StoreAllFrom(Dict<K, V> other)
        {
            var cursor = other.CreateCursor();
            while (cursor.MoveNext())
            {
                if (cursor.Slot == other._nullSlot)
                {
                    StoreNull(cursor.Value);
                }
                else
                {
                    this[cursor.Key] = cursor.Value;
                }
            }
        }

        public Dict<K, V> Merge(Dict<K, V> other)
        {
            var newDict = Copy();
            newDict.Update(other);

            return newDict;
        }
    }

    /// <summary>
    /// Non-generic helper for <see cref="Dict{K, V}"/> providing static entry points
    /// (e.g. <c>dict.fromkeys(...)</c>) that dispatch to the generic type.
    /// </summary>
    /// <remarks>
    /// This class exists alongside the generic <see cref="Dict{K, V}"/> class via
    /// arity-based overloading so that generated C# code like <c>Dict.Fromkeys(...)</c>
    /// can resolve without naming the type arguments.
    /// </remarks>
    public static class Dict
    {
        /// <summary>
        /// Create a new dictionary with keys from <paramref name="keys"/> and values set to <c>null</c>.
        /// Mirrors Python's <c>dict.fromkeys(iterable)</c>.
        /// </summary>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <param name="keys">An iterable of keys for the new dictionary.</param>
        /// <returns>A new dictionary with the specified keys all mapped to <c>null</c>.</returns>
        /// <example>
        /// <code>
        /// d = dict.fromkeys(["a", "b"])    # {"a": None, "b": None}
        /// </code>
        /// </example>
        public static Dict<TKey, object> Fromkeys<TKey>(IEnumerable<TKey> keys)
            where TKey : notnull
        {
            // python's None, stored in an object-valued dict (the argument, not a parameter default).
            return Dict<TKey, object>.Fromkeys(keys, null!);
        }

        /// <summary>
        /// Create a new dictionary with keys from <paramref name="keys"/> and values set to <paramref name="value"/>.
        /// Mirrors Python's <c>dict.fromkeys(iterable, value)</c>. All keys share the same value reference.
        /// </summary>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="keys">An iterable of keys for the new dictionary.</param>
        /// <param name="value">The value assigned to every key.</param>
        /// <returns>A new dictionary with the specified keys all mapped to <paramref name="value"/>.</returns>
        /// <example>
        /// <code>
        /// d = dict.fromkeys(["a", "b"], 0)    # {"a": 0, "b": 0}
        /// </code>
        /// </example>
        public static Dict<TKey, TValue> Fromkeys<TKey, TValue>(IEnumerable<TKey> keys, TValue value)
            where TKey : notnull
        {
            return Dict<TKey, TValue>.Fromkeys(keys, value);
        }
    }
}
