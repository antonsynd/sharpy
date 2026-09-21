using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// A ChainMap groups multiple dictionaries together to create a single, updateable view.
    /// Like Python's collections.ChainMap.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="ISized"/> (<c>__len__</c> → <c>len(cm)</c>, the number of unique keys)
    /// and <see cref="IEnumerable{T}"/> over the KEYS (<c>__iter__</c> → <c>for k in cm</c>,
    /// <c>list(cm)</c>). Keys are the only generic <c>IEnumerable</c> the type exposes so
    /// <c>list(cm)</c> binds <c>Builtins.List&lt;K&gt;(IEnumerable&lt;K&gt;)</c>; pairs are reached
    /// through <see cref="Items"/> only (#1933). Keys/values/items iterate the maps in CPython's
    /// REVERSED order — the last map is walked first and dedup keeps the first occurrence in that
    /// reversed walk (for maps <c>{n,x},{n,y}</c> CPython yields keys <c>n, y, x</c>) — while VALUE
    /// lookup stays first-map-wins via the indexer.
    /// </remarks>
    [SharpyModuleType("collections", "ChainMap")]
    public class ChainMap<K, V> : ISized, IEnumerable<K>, IEquatable<ChainMap<K, V>> where K : notnull
    {
        private readonly System.Collections.Generic.List<Dict<K, V>> _maps;

        /// <summary>
        /// Create a new ChainMap from the given dictionaries.
        /// If no maps are provided, a single empty dictionary is used.
        /// </summary>
        public ChainMap(params Dict<K, V>[] maps)
        {
            if (maps == null || maps.Length == 0)
            {
                _maps = new System.Collections.Generic.List<Dict<K, V>> { new Dict<K, V>() };
            }
            else
            {
                _maps = new System.Collections.Generic.List<Dict<K, V>>(maps);
            }
        }

        /// <summary>
        /// The list of underlying mappings.
        /// </summary>
        public List<Dict<K, V>> Maps => new List<Dict<K, V>>(_maps);

        /// <summary>
        /// A new ChainMap containing all maps except the first one.
        /// </summary>
        public ChainMap<K, V> Parents
        {
            get
            {
                if (_maps.Count <= 1)
                {
                    return new ChainMap<K, V>();
                }
                var parentMaps = new Dict<K, V>[_maps.Count - 1];
                for (int i = 1; i < _maps.Count; i++)
                {
                    parentMaps[i - 1] = _maps[i];
                }
                return new ChainMap<K, V>(parentMaps);
            }
        }

        /// <summary>
        /// Return a new ChainMap with a new map followed by all previous maps.
        /// If no map is provided, an empty dict is used.
        /// </summary>
        public ChainMap<K, V> NewChild(Dict<K, V>? m = null)
        {
            var child = m ?? new Dict<K, V>();
            var allMaps = new Dict<K, V>[_maps.Count + 1];
            allMaps[0] = child;
            for (int i = 0; i < _maps.Count; i++)
            {
                allMaps[i + 1] = _maps[i];
            }
            return new ChainMap<K, V>(allMaps);
        }

        /// <summary>
        /// Gets or sets a value. Gets search through all maps; sets go to the first map.
        /// </summary>
        public V this[K key]
        {
            get
            {
                foreach (var map in _maps)
                {
                    if (map.ContainsKey(key))
                    {
                        return map[key];
                    }
                }
                throw new KeyError(key?.ToString() ?? "None");
            }
            set
            {
                _maps[0][key] = value;
            }
        }

        /// <summary>
        /// Check if any map contains the key.
        /// </summary>
        public bool ContainsKey(K key)
        {
            foreach (var map in _maps)
            {
                if (map.ContainsKey(key))
                {
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc cref="ContainsKey"/>
        public bool Contains(K key) => ContainsKey(key);

        /// <summary>
        /// Get a value, searching through all maps.
        /// </summary>
        public V Get(K key, V @default = default!)
        {
            foreach (var map in _maps)
            {
                if (map.ContainsKey(key))
                {
                    return map[key];
                }
            }
            return @default;
        }

        /// <summary>
        /// Return all unique keys across all maps, in CPython's merge order (maps walked in REVERSED
        /// order, dedup keeping the first occurrence in that walk).
        /// </summary>
        private IEnumerable<K> EnumerateKeys()
        {
            var seen = new System.Collections.Generic.HashSet<K>();
            for (int i = _maps.Count - 1; i >= 0; i--)
            {
                foreach (var key in _maps[i])
                {
                    if (seen.Add(key))
                    {
                        yield return key;
                    }
                }
            }
        }

        /// <summary>
        /// Return all unique keys across all maps as a sized list, in CPython's merge order.
        /// </summary>
        public List<K> Keys()
        {
            return new List<K>(EnumerateKeys());
        }

        /// <summary>
        /// Return the values for the unique keys as a sized list, in CPython's merge key order.
        /// Each value is the first-map-wins lookup for its key.
        /// </summary>
        public List<V> Values()
        {
            return new List<V>(EnumerateKeys().Select(key => this[key]));
        }

        /// <summary>
        /// Return the (key, value) pairs as a sized list, in CPython's merge key order.
        /// Each value is the first-map-wins lookup for its key.
        /// </summary>
        public List<(K, V)> Items()
        {
            return new List<(K, V)>(EnumerateKeys().Select(key => (key, this[key])));
        }

        /// <summary>
        /// Gets the total number of unique keys across all maps.
        /// </summary>
        public int Count
        {
            get
            {
                var seen = new System.Collections.Generic.HashSet<K>();
                foreach (var map in _maps)
                {
                    foreach (var key in map)
                    {
                        seen.Add(key);
                    }
                }
                return seen.Count;
            }
        }

        /// <summary>
        /// Remove key from the first mapping. Raises KeyError if not found in first mapping.
        /// </summary>
        public V Pop(K key)
        {
            return _maps[0].Pop(key);
        }

        /// <summary>
        /// Clear the first mapping.
        /// </summary>
        public void Clear()
        {
            _maps[0].Clear();
        }

        /// <summary>
        /// Iterate the unique keys in CPython's merge order (Python's <c>__iter__</c>). Pairs are
        /// reached through <see cref="Items"/>.
        /// </summary>
        public IEnumerator<K> GetEnumerator() => EnumerateKeys().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>The (key, value) pairs (first-map-wins values), for equality comparison.</summary>
        internal IEnumerable<KeyValuePair<K, V>> PairsForEquality()
            => EnumerateKeys().Select(k => new KeyValuePair<K, V>(k, this[k]));

        // ── Equality (Python __eq__): compare by contents (a ChainMap equals a mapping with the
        //    same flattened key/value pairs). Declared as operator ==/!= so the checker discovers
        //    them through the same CLR op_Equality path Dict<K,V> uses (Dict.cs:286) — no arm (#1933).

        /// <summary>Content equality with another ChainMap (flattened key/value pairs).</summary>
        public bool Equals(ChainMap<K, V>? other)
            => other is not null && MappingEquality.PairwiseEquals(Count, PairsForEquality(), other.Count, other.PairsForEquality());

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ChainMap<K, V> other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Count;

        /// <summary>ChainMap == ChainMap — pairwise on flattened contents.</summary>
        public static bool operator ==(ChainMap<K, V>? left, ChainMap<K, V>? right)
            => left is null ? right is null : left.Equals(right);

        /// <summary>ChainMap != ChainMap.</summary>
        public static bool operator !=(ChainMap<K, V>? left, ChainMap<K, V>? right) => !(left == right);

        /// <summary>ChainMap == dict — pairwise, matching CPython.</summary>
        public static bool operator ==(ChainMap<K, V>? left, Dict<K, V>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left.PairsForEquality(), right.Count, right);

        /// <summary>ChainMap != dict.</summary>
        public static bool operator !=(ChainMap<K, V>? left, Dict<K, V>? right) => !(left == right);

        /// <summary>dict == ChainMap — pairwise, delegating to the symmetric overload.</summary>
        public static bool operator ==(Dict<K, V>? left, ChainMap<K, V>? right) => right == left;

        /// <summary>dict != ChainMap.</summary>
        public static bool operator !=(Dict<K, V>? left, ChainMap<K, V>? right) => !(left == right);
    }
}
