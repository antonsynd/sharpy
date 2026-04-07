using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// A ChainMap groups multiple dictionaries together to create a single, updateable view.
    /// Like Python's collections.ChainMap.
    /// </summary>
    [SharpyModuleType("collections")]
    public class ChainMap<K, V> where K : notnull
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
        public System.Collections.Generic.List<Dict<K, V>> Maps => _maps;

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
        /// Return all unique keys across all maps.
        /// </summary>
        public IEnumerable<K> Keys()
        {
            var seen = new System.Collections.Generic.HashSet<K>();
            foreach (var map in _maps)
            {
                foreach (var key in map)
                {
                    if (seen.Add(key))
                    {
                        yield return key;
                    }
                }
            }
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
    }
}
