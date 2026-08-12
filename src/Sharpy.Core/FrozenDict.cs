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
    /// <para>
    /// Backed by <see cref="ImmutableDictionary{TKey, TValue}"/> for lookup and by an
    /// <see cref="ImmutableArray{T}"/> of keys for ORDER, both available on .NET Standard 2.1.
    /// Provides a Python-style API on top of the immutable .NET types.
    /// </para>
    /// <para>
    /// The key array is what makes iteration insertion-ordered, matching <c>dict</c> (#1392).
    /// <see cref="ImmutableDictionary{TKey, TValue}"/> alone is a hash-array-mapped trie with no
    /// defined enumeration order, and .NET randomizes string hash seeds per process — so before
    /// this, the same program printed a two-key frozendict in a different order run to run, and no
    /// fixture could pin a multi-key <c>repr</c>. Every enumeration path below walks
    /// <see cref="_order"/>; the dictionary is consulted only for lookup.
    /// </para>
    /// <para>
    /// EQUALITY AND HASHING DELIBERATELY DO NOT READ THE ORDER. Two frozendicts with the same pairs
    /// inserted in different orders are equal and hash alike, which is what lets a frozendict be a
    /// dict key or a set element — the type's entire reason to exist. Order is a rendering and
    /// iteration property only. <c>repr</c> may differ where <c>==</c> says equal; that is the same
    /// split <c>dict</c> has.
    /// </para>
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

        /// <summary>
        /// The keys in insertion order. Always the same length as <see cref="_dict"/> and holding
        /// exactly its keys — the two are built together and never assigned apart.
        /// </summary>
        private readonly ImmutableArray<TKey> _order;

        /// <summary>Create an empty frozendict.</summary>
        public FrozenDict()
        {
            _dict = ImmutableDictionary<TKey, TValue>.Empty;
            _order = ImmutableArray<TKey>.Empty;
        }

        /// <summary>Create a frozendict from an iterable of key-value pairs.</summary>
        /// <remarks>
        /// Later duplicate keys overwrite earlier ones, matching Python semantics — and the repeated
        /// key keeps its FIRST position in iteration order, which is also Python's rule (#1392).
        /// </remarks>
        public FrozenDict(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            if (items is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            Build(items, out _dict, out _order);
        }

        /// <summary>Create a frozendict from an existing <see cref="Dict{K, V}"/>.</summary>
        public FrozenDict(Dict<TKey, TValue> dict)
        {
            if (dict is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            Build((IEnumerable<KeyValuePair<TKey, TValue>>)dict, out _dict, out _order);
        }

        // Private constructor for internal operations (e.g. union).
        private FrozenDict(ImmutableDictionary<TKey, TValue> dict, ImmutableArray<TKey> order)
        {
            _dict = dict;
            _order = order;
        }

        /// <summary>
        /// Builds the lookup map and the insertion-ordered key array from one pass over
        /// <paramref name="items"/>. Both outputs come from here so they cannot disagree.
        /// </summary>
        /// <remarks>
        /// A repeated key overwrites the value but KEEPS ITS ORIGINAL POSITION, which is CPython's
        /// rule and was verified rather than assumed: <c>dict([('a',1),('b',2),('a',3)])</c> is
        /// <c>{'a': 3, 'b': 2}</c>, not <c>{'b': 2, 'a': 3}</c>. Appending on every write instead
        /// would put <c>a</c> last and duplicate it in the array.
        /// </remarks>
        private static void Build(
            IEnumerable<KeyValuePair<TKey, TValue>> items,
            out ImmutableDictionary<TKey, TValue> dict,
            out ImmutableArray<TKey> order)
        {
            var mapBuilder = ImmutableDictionary.CreateBuilder<TKey, TValue>();
            var orderBuilder = ImmutableArray.CreateBuilder<TKey>();

            foreach (var kv in items)
            {
                if (!mapBuilder.ContainsKey(kv.Key))
                {
                    orderBuilder.Add(kv.Key);
                }

                mapBuilder[kv.Key] = kv.Value;
            }

            dict = mapBuilder.ToImmutable();
            order = orderBuilder.ToImmutable();
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
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _order;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values();

        /// <summary>Returns an iterable of the frozendict's keys. Python: <c>d.keys()</c>.</summary>
        public IEnumerable<TKey> Keys() => _order;

        /// <summary>Returns an iterable of the frozendict's values. Python: <c>d.values()</c>.</summary>
        public IEnumerable<TValue> Values()
        {
            foreach (var key in _order)
            {
                yield return _dict[key];
            }
        }

        /// <summary>Returns the key-value pairs as an iterable of tuples.</summary>
        public IEnumerable<(TKey, TValue)> Items()
        {
            foreach (var key in _order)
            {
                yield return (key, _dict[key]);
            }
        }

        /// <summary>Attempts to get the value associated with the specified key.</summary>
        public bool TryGetValue(TKey key, out TValue value) => _dict.TryGetValue(key, out value!);

        /// <summary>
        /// Returns an enumerator that iterates through the keys (Python semantics).
        /// </summary>
        public IEnumerator<TKey> GetEnumerator() => Keys().GetEnumerator();

        /// <summary>Returns an enumerator that iterates through the key/value pairs.</summary>
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
            => Pairs().GetEnumerator();

        /// <summary>The key/value pairs in insertion order.</summary>
        private IEnumerable<KeyValuePair<TKey, TValue>> Pairs()
        {
            foreach (var key in _order)
            {
                yield return new KeyValuePair<TKey, TValue>(key, _dict[key]);
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Return a shallow copy. Because a frozendict is immutable, this
        /// returns the same instance (matching CPython's <c>frozenset.copy()</c>
        /// behavior for immutable containers).
        /// </summary>
        public FrozenDict<TKey, TValue> Copy() => this;

        /// <summary>
        /// Merge with a dict. The LEFT operand decides the result type, by analogy with
        /// set/frozenset (#1312): a frozendict on the left yields a frozendict. Keys on the right
        /// win, matching this type's own <c>|</c>.
        /// </summary>
        public static FrozenDict<TKey, TValue> operator |(
            FrozenDict<TKey, TValue> left,
            Dict<TKey, TValue> right)
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            return Merge(left, right.Items());
        }

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

            return Merge(left, right.Items());
        }

        /// <summary>
        /// <paramref name="left"/> merged with <paramref name="rightItems"/>, right values winning.
        /// </summary>
        /// <remarks>
        /// The ORDER rule is <c>dict</c>'s, verified against CPython rather than assumed:
        /// <c>{'a':1,'b':2} | {'b':9,'c':3}</c> is <c>{'a': 1, 'b': 9, 'c': 3}</c> — a key present on
        /// both sides keeps its LEFT position and takes the RIGHT value, and keys new on the right
        /// append in their own order. Rebuilding from left-then-right pairs gets exactly that,
        /// because <see cref="Build"/> already keeps a repeated key's first position.
        /// </remarks>
        private static FrozenDict<TKey, TValue> Merge(
            FrozenDict<TKey, TValue> left,
            IEnumerable<(TKey, TValue)> rightItems)
        {
            var pairs = new System.Collections.Generic.List<KeyValuePair<TKey, TValue>>();

            foreach (var kv in left.Pairs())
            {
                pairs.Add(kv);
            }

            foreach (var (key, value) in rightItems)
            {
                pairs.Add(new KeyValuePair<TKey, TValue>(key, value));
            }

            Build(pairs, out var dict, out var order);
            return new FrozenDict<TKey, TValue>(dict, order);
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

            foreach (var kv in Pairs())
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
