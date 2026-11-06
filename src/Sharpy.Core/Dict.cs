using System.Collections.Generic;
using System;

namespace Sharpy
{
    using System.Text;

    using static Builtins;

    /// <summary>
    /// A dictionary that maps keys to values, similar to Python's <c>dict</c>.
    /// Supports Python-style methods: get, keys, values, items, pop, update, setdefault.
    /// </summary>
    /// <typeparam name="K">The type of keys</typeparam>
    /// <typeparam name="V">The type of values</typeparam>
    public sealed partial class Dict<K, V>
        : IDictionary<K, V>,
          IReadOnlyDictionary<K, V>,
          System.Collections.IDictionary,
          System.IEquatable<Dict<K, V>>,
          ISized,
          IDeepCopyable,
          IShallowCopyable
        where K : notnull
    {
        /// <summary>Create an empty dictionary.</summary>
        public Dict()
        {
            _index = new Dictionary<K, int>();
            _entries = Array.Empty<Entry>();
        }

        /// <summary>
        /// Create an empty dictionary whose backing storage is pre-sized to hold at least
        /// <paramref name="capacity"/> entries without rehashing. Used by comprehension
        /// lowering when the source size is known up front; not part of Python's <c>dict</c> API.
        /// </summary>
        public Dict(int capacity)
        {
            int size = capacity < 0 ? 0 : capacity;
            _index = new Dictionary<K, int>(size);
            _entries = size == 0 ? Array.Empty<Entry>() : new Entry[size];
        }

        /// <summary>Create a dictionary from an existing mapping.</summary>
        public Dict(IReadOnlyDictionary<K, V> mapping) : this()
        {
            if (mapping is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            foreach (var kvp in mapping)
            {
                this[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Create a dictionary from any sequence of key-value pairs. This is the constructor that
        /// materializes a .NET mapping — <c>IDictionary&lt;K,V&gt;</c> as well as
        /// <c>IReadOnlyDictionary&lt;K,V&gt;</c> — entering a Sharpy <c>dict</c> slot, since both are
        /// <c>IEnumerable&lt;KeyValuePair&lt;K,V&gt;&gt;</c> (#1866). The
        /// <see cref="Dict(IReadOnlyDictionary{K, V})"/> overload stays and wins C# betterness for
        /// values that are both.
        /// </summary>
        public Dict(IEnumerable<KeyValuePair<K, V>> pairs) : this()
        {
            if (pairs is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            foreach (var kvp in pairs)
            {
                this[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>Create a dictionary from an iterable of key-value tuples.</summary>
        public Dict(IEnumerable<(K, V)> iterable) : this()
        {
            if (iterable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            foreach (var (key, value) in iterable)
            {
                this[key] = value;
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
                dict[kvp.Key] = kvp.Value;
            }
            return dict;
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <exception cref="KeyError">Thrown if the key does not exist on get.</exception>
        public V this[K key]
        {
            get
            {
                int slot = FindSlot(key);
                if (slot < 0)
                {
                    throw Sharpy.Index.KeyNotFound(key);
                }

                return _entries[slot].Value;
            }
            set
            {
                int slot = FindSlot(key);
                if (slot >= 0)
                {
                    _entries[slot].Value = value;
                }
                else
                {
                    InsertNew(key, value);
                }
            }
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

            if (Count != other.Count)
            {
                return false;
            }

            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                int otherSlot = cursor.Slot == _nullSlot ? other._nullSlot : other.FindSlot(cursor.Key);
                if (otherSlot < 0 || !Operator.Eq(cursor.Value, other._entries[otherSlot].Value))
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

        /// <summary>Returns a new merged dictionary (union operator).</summary>
        public static Dict<K, V> operator |(Dict<K, V> left, Dict<K, V> right)
        {
            return left.Merge(right);
        }

        /// <summary>
        /// Merge with a frozendict. The LEFT operand decides the result type, by analogy with
        /// set/frozenset (#1312): a dict on the left yields a dict. Keys on the right win, matching
        /// this type's own <c>|</c>.
        /// </summary>
        public static Dict<K, V> operator |(Dict<K, V> left, FrozenDict<K, V> right)
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            var merged = left.Copy();
            foreach (var key in right.Keys())
            {
                merged[key] = right[key];
            }

            return merged;
        }

        /// <summary>Determines whether two dictionaries are equal.</summary>
        public static bool operator ==(Dict<K, V>? left, Dict<K, V>? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        /// <summary>Determines whether two dictionaries are not equal.</summary>
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
                hash = hash * 31 + _index.GetHashCode();
                if (_nullSlot >= 0)
                {
                    hash = hash * 31 + (_entries[_nullSlot].Value?.GetHashCode() ?? 0);
                }
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

            int written = 0;
            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                if (written > 0)
                    builder.Append(", ");
                builder.Append($"{Repr(cursor.Key)}: {Repr(cursor.Value)}");
                ++written;
            }

            builder.Append('}');

            return builder.ToString();
        }

        /// <summary>Returns true if the dictionary is non-empty.</summary>
        public static bool operator true(Dict<K, V>? dict)
        {
            return dict is not null && dict.Count > 0;
        }

        /// <summary>Returns true if the dictionary is empty or null.</summary>
        public static bool operator false(Dict<K, V>? dict)
        {
            return dict is null || dict.Count == 0;
        }

        /// <inheritdoc/>
        object IDeepCopyable.DeepCopy(Dictionary<object, object> memo)
        {
            var newDict = new Dict<K, V>();
            memo[this] = newDict;

            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                V copiedValue = cursor.Value != null
                    ? (V)CopyModule.DeepCopyInternal(cursor.Value, memo)
                    : cursor.Value;

                if (cursor.Slot == _nullSlot)
                {
                    newDict.StoreNull(copiedValue);
                }
                else
                {
                    newDict[(K)CopyModule.DeepCopyInternal(cursor.Key, memo)] = copiedValue;
                }
            }

            return newDict;
        }
    }
}
