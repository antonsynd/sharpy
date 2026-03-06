using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// A ChainMap groups multiple dicts together to create a single, updateable view.
    /// Lookups search the underlying mappings successively until a key is found.
    /// Writes, updates, and deletions only operate on the first mapping.
    /// </summary>
    public class ChainMap<TKey, TValue> where TKey : notnull
    {
        private readonly System.Collections.Generic.List<System.Collections.Generic.Dictionary<TKey, TValue>> _maps;

        public ChainMap(params System.Collections.Generic.Dictionary<TKey, TValue>[] maps)
        {
            if (maps.Length == 0)
            {
                _maps = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<TKey, TValue>>
                {
                    new System.Collections.Generic.Dictionary<TKey, TValue>()
                };
            }
            else
            {
                _maps = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<TKey, TValue>>(maps);
            }
        }

        /// <summary>
        /// The list of underlying mappings.
        /// </summary>
        public IReadOnlyList<System.Collections.Generic.Dictionary<TKey, TValue>> Maps => _maps;

        /// <summary>
        /// New ChainMap containing all maps except the first.
        /// </summary>
        public ChainMap<TKey, TValue> Parents
        {
            get
            {
                if (_maps.Count <= 1)
                {
                    return new ChainMap<TKey, TValue>();
                }

                return new ChainMap<TKey, TValue>(_maps.Skip(1).ToArray());
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                foreach (var map in _maps)
                {
                    if (map.TryGetValue(key, out TValue? value))
                    {
                        return value;
                    }
                }

                throw new KeyError($"'{key}'");
            }
            set
            {
                _maps[0][key] = value;
            }
        }

        public bool ContainsKey(TKey key)
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

        public TValue Get(TKey key, TValue defaultValue = default!)
        {
            foreach (var map in _maps)
            {
                if (map.TryGetValue(key, out TValue? value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Return a new ChainMap with a new map followed by all previous maps.
        /// </summary>
        public ChainMap<TKey, TValue> NewChild(System.Collections.Generic.Dictionary<TKey, TValue>? m = null)
        {
            var newMap = m ?? new System.Collections.Generic.Dictionary<TKey, TValue>();
            var allMaps = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<TKey, TValue>> { newMap };
            allMaps.AddRange(_maps);
            return new ChainMap<TKey, TValue>(allMaps.ToArray());
        }

        public IEnumerable<TKey> Keys
        {
            get
            {
                var seen = new System.Collections.Generic.HashSet<TKey>();
                foreach (var map in _maps)
                {
                    foreach (var key in map.Keys)
                    {
                        if (seen.Add(key))
                        {
                            yield return key;
                        }
                    }
                }
            }
        }

        public int Count => Keys.Count();

        public bool Remove(TKey key) => _maps[0].Remove(key);
    }
}
