using System.Collections;
using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public sealed partial class Dict<K, V> : MutableMapping<K, V> where K : notnull
    {
        private readonly Dictionary<K, V> _dict;

        public Dict()
        {
            _dict = [];
        }

        public Dict(Mapping<K, V> mapping) : this()
        {
            if (mapping is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }
        }

        public Dict(Iterable<(K, V)> iterable) : this()
        {
            if (iterable is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }
        }

        /// <summary>
        /// Return a shallow copy of the dictionary.
        /// </summary>
        public Dict<K, V> Copy()
        {
            var newDict = new Dict<K, V>();
            newDict._dict.EnsureCapacity(_dict.Count);

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

        public bool Contains(K k)
        {
            return __Contains__(k);
        }

        public V? Get(K k)
        {
            if (_dict.TryGetValue(k, out V? value))
            {
                return value;
            }

            return default;
        }

        public V Get(K k, V @default)
        {
            if (_dict.TryGetValue(k, out V? value))
            {
                return value;
            }

            return @default;
        }

        public IEnumerator<K> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public ItemsView<K, V> Items()
        {
            throw new NotImplementedException();
        }

        public KeysView<K> Keys()
        {
            throw new NotImplementedException();
        }

        public V Pop(K k)
        {
            if (_dict.Remove(k, out V? value))
            {
                return value;
            }

            throw new KeyError(Repr(k));
        }

        public V Pop(K k, V @default)
        {
            if (_dict.Remove(k, out V? value))
            {
                return value;
            }

            return @default;
        }

        public (K, V) PopItem()
        {
            throw new NotImplementedException();
        }

        public V SetDefault(K k, V @default)
        {
            throw new NotImplementedException();
        }

        public void Update(Mapping<K, V> other)
        {
            throw new NotImplementedException();
        }

        public void Update(Iterable<(K, V)> other)
        {
            throw new NotImplementedException();
        }

        public ValuesView<V> Values()
        {
            throw new NotImplementedException();
        }

        public bool __Contains__(K k)
        {
            return _dict.ContainsKey(k);
        }

        public void __DelItem__(K k)
        {
            if (!_dict.Remove(k))
            {
                throw new KeyError(Repr(k));
            }
        }

        public V __GetItem__(K k)
        {
            if (_dict.TryGetValue(k, out V? value))
            {
                return value;
            }

            throw new KeyError(Repr(k));
        }

        public Iterator<K> __Iter__()
        {
            throw new NotImplementedException();
        }

        public uint __Len__()
        {
            return (uint)_dict.Count;
        }

        public void __SetItem__(K k, V v)
        {
            _dict[k] = v;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Dictionary<K, V> ToDictionary()
        {
            return new Dictionary<K, V>(_dict);
        }
    }
}
