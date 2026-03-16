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
          System.IEquatable<Dict<K, V>>,
          ISized,
          IDeepCopyable,
          IShallowCopyable
        where K : notnull
    {
        private readonly Dictionary<K, V> _dict;

        /// <summary>Create an empty dictionary.</summary>
        public Dict()
        {
            _dict = new Dictionary<K, V>();
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
                _dict[kvp.Key] = kvp.Value;
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

        /// <summary>Returns a new merged dictionary (union operator).</summary>
        public static Dict<K, V> operator |(Dict<K, V> left, Dict<K, V> right)
        {
            return left.Merge(right);
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

        /// <summary>Returns true if the dictionary is non-empty.</summary>
        public static bool operator true(Dict<K, V>? dict)
        {
            return dict is not null && dict._dict.Count > 0;
        }

        /// <summary>Returns true if the dictionary is empty or null.</summary>
        public static bool operator false(Dict<K, V>? dict)
        {
            return dict is null || dict._dict.Count == 0;
        }

        /// <inheritdoc/>
        object IDeepCopyable.DeepCopy(Dictionary<object, object> memo)
        {
            var newDict = new Dict<K, V>();
            memo[this] = newDict;

            foreach (var kvp in _dict)
            {
                K copiedKey = (K)CopyModule.DeepCopyInternal(kvp.Key, memo);
                V copiedValue = kvp.Value != null
                    ? (V)CopyModule.DeepCopyInternal(kvp.Value, memo)
                    : kvp.Value;

                newDict._dict[copiedKey] = copiedValue;
            }

            return newDict;
        }
    }
}
