using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Sharpy
{
    using static Builtins;

    /// <summary>
    /// An immutable, hashable mapping of keys to values. Since frozendict is
    /// immutable, it can be used as a dictionary key or as an element of a
    /// set, similar to Python's proposed <c>frozendict</c> (PEP 814).
    /// </summary>
    /// <remarks>
    /// Backed by <see cref="ImmutableDictionary{TKey, TValue}"/> for
    /// .NET Standard 2.0/2.1 compatibility. Provides a Python-style API on
    /// top of the immutable .NET type.
    /// </remarks>
    /// <typeparam name="TKey">The type of keys.</typeparam>
    /// <typeparam name="TValue">The type of values.</typeparam>
    public sealed class FrozenDict<TKey, TValue>
        : IReadOnlyDictionary<TKey, TValue>,
          IEquatable<FrozenDict<TKey, TValue>>,
          ISized
        where TKey : notnull
    {
        private readonly ImmutableDictionary<TKey, TValue> _dict;

        /// <summary>Create an empty frozendict.</summary>
        public FrozenDict()
        {
            _dict = ImmutableDictionary<TKey, TValue>.Empty;
        }

        /// <summary>Create a frozendict from an iterable of key-value pairs.</summary>
        /// <remarks>Later duplicate keys overwrite earlier ones, matching Python semantics.</remarks>
        public FrozenDict(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            if (items is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            var builder = ImmutableDictionary.CreateBuilder<TKey, TValue>();

            foreach (var kv in items)
            {
                builder[kv.Key] = kv.Value;
            }

            _dict = builder.ToImmutable();
        }

        /// <summary>Create a frozendict from an existing <see cref="Dict{K, V}"/>.</summary>
        public FrozenDict(Dict<TKey, TValue> dict)
        {
            if (dict is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            var builder = ImmutableDictionary.CreateBuilder<TKey, TValue>();

            foreach (var kv in (IEnumerable<KeyValuePair<TKey, TValue>>)dict)
            {
                builder[kv.Key] = kv.Value;
            }

            _dict = builder.ToImmutable();
        }

        // Private constructor for internal operations (e.g. union).
        private FrozenDict(ImmutableDictionary<TKey, TValue> dict)
        {
            _dict = dict;
        }

        /// <summary>Gets the number of key/value pairs in the frozendict.</summary>
        public int Count => _dict.Count;

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <exception cref="KeyError">Thrown if the key does not exist.</exception>
        public TValue this[TKey key]
        {
            get
            {
                if (_dict.TryGetValue(key, out TValue? value))
                {
                    return value;
                }

                throw new KeyError(Repr(key));
            }
        }

        /// <summary>Determines whether the frozendict contains the specified key.</summary>
        public bool ContainsKey(TKey key) => _dict.ContainsKey(key);

        /// <summary>
        /// Compiler hook: supports <c>key in frozendict</c> expressions.
        /// </summary>
        public bool Contains(TKey key) => _dict.ContainsKey(key);

        /// <summary>
        /// Return the value for <paramref name="key"/> if present, otherwise
        /// <paramref name="default"/>. Mirrors Python's <c>dict.get(key, default=None)</c>.
        /// </summary>
        public TValue Get(TKey key, TValue @default = default!)
        {
            if (_dict.TryGetValue(key, out TValue? value))
            {
                return value;
            }

            return @default;
        }

        // Explicit interface implementations satisfy IReadOnlyDictionary<TKey, TValue>
        // while keeping the Sharpy-style method API (Keys() / Values()) public.
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _dict.Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _dict.Values;

        /// <summary>Returns an iterable of the frozendict's keys. Python: <c>d.keys()</c>.</summary>
        public IEnumerable<TKey> Keys() => _dict.Keys;

        /// <summary>Returns an iterable of the frozendict's values. Python: <c>d.values()</c>.</summary>
        public IEnumerable<TValue> Values() => _dict.Values;

        /// <summary>Returns the key-value pairs as an iterable of tuples.</summary>
        public IEnumerable<(TKey, TValue)> Items()
        {
            foreach (var kv in _dict)
            {
                yield return (kv.Key, kv.Value);
            }
        }

        /// <summary>Attempts to get the value associated with the specified key.</summary>
        public bool TryGetValue(TKey key, out TValue value) => _dict.TryGetValue(key, out value!);

        /// <summary>
        /// Returns an enumerator that iterates through the keys (Python semantics).
        /// </summary>
        public IEnumerator<TKey> GetEnumerator() => _dict.Keys.GetEnumerator();

        /// <summary>Returns an enumerator that iterates through the key/value pairs.</summary>
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
            => _dict.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Return a shallow copy. Because a frozendict is immutable, this
        /// returns the same instance (matching CPython's <c>frozenset.copy()</c>
        /// behavior for immutable containers).
        /// </summary>
        public FrozenDict<TKey, TValue> Copy() => this;

        /// <summary>
        /// Return a new frozendict with the merged contents of <paramref name="right"/>,
        /// where keys in <paramref name="right"/> overwrite keys from <paramref name="left"/>.
        /// </summary>
        public static FrozenDict<TKey, TValue> operator |(
            FrozenDict<TKey, TValue> left,
            FrozenDict<TKey, TValue> right)
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            var builder = left._dict.ToBuilder();

            foreach (var kv in right._dict)
            {
                builder[kv.Key] = kv.Value;
            }

            return new FrozenDict<TKey, TValue>(builder.ToImmutable());
        }

        /// <summary>
        /// Determines whether this frozendict is equal to another frozendict
        /// by comparing all key-value pairs (order-independent).
        /// </summary>
        public bool Equals(FrozenDict<TKey, TValue>? other)
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
                if (!other._dict.TryGetValue(kv.Key, out TValue? value))
                {
                    return false;
                }

                if (!Operator.Eq(kv.Value!, value!))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is FrozenDict<TKey, TValue> other && Equals(other);
        }

        /// <summary>
        /// Returns an order-independent hash code based on the key-value pairs.
        /// Two frozendicts that compare equal produce the same hash.
        /// </summary>
        public override int GetHashCode()
        {
            // XOR of per-pair hashes -> order-independent and consistent with Equals.
            int hash = 0;

            foreach (var kv in _dict)
            {
                int keyHash = kv.Key?.GetHashCode() ?? 0;
                int valueHash = kv.Value?.GetHashCode() ?? 0;

                unchecked
                {
                    hash ^= (keyHash * 397) ^ valueHash;
                }
            }

            return hash;
        }

        /// <summary>Determines whether two frozendicts are equal.</summary>
        public static bool operator ==(FrozenDict<TKey, TValue>? left, FrozenDict<TKey, TValue>? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        /// <summary>Determines whether two frozendicts are not equal.</summary>
        public static bool operator !=(FrozenDict<TKey, TValue>? left, FrozenDict<TKey, TValue>? right)
        {
            return !(left == right);
        }

        /// <summary>Returns true if the frozendict is non-empty.</summary>
        public static bool operator true(FrozenDict<TKey, TValue>? dict)
        {
            return dict is not null && dict._dict.Count > 0;
        }

        /// <summary>Returns true if the frozendict is empty or null.</summary>
        public static bool operator false(FrozenDict<TKey, TValue>? dict)
        {
            return dict is null || dict._dict.Count == 0;
        }

        /// <summary>Returns a string representation of the frozendict.</summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append("frozendict({");

            int i = 0;

            foreach (var kv in _dict)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append($"{Repr(kv.Key)}: {Repr(kv.Value)}");
                ++i;
            }

            builder.Append("})");

            return builder.ToString();
        }
    }
}
