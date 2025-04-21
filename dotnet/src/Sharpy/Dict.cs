namespace Sharpy;

using System.Collections;
using System.Text;

using Collections.Interfaces;
using Operator;

public sealed partial class Dict<K, V> : Object, IMutableMapping<K, V> where K : notnull
{
    private readonly Dictionary<K, V> _dict;

    public Dict()
    {
        _dict = [];
    }

    public Dict(IMapping<K, V> mapping) : this()
    {
        if (mapping is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }
    }

    public Dict(IIterable<(K, V)> iterable) : this()
    {
        if (iterable is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
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

    public bool Contains(K key)
    {
        return __Contains__(key);
    }

    public V? Get(K key)
    {
        if (_dict.TryGetValue(key, out V? value))
        {
            return value;
        }

        return default;
    }

    public V Get(K key, V @default)
    {
        if (_dict.TryGetValue(key, out V? value))
        {
            return value;
        }

        return @default;
    }

    public IEnumerator<K> GetEnumerator()
    {
        foreach (var key in _dict.Keys)
        {
            yield return key;
        }
    }

    public IItemsView<K, V> Items()
    {
        throw new NotImplementedException();
    }

    public IKeysView<K> Keys()
    {
        return new DictKeyView<K, V>(_dict.Keys);
    }

    public V Pop(K key)
    {
        if (_dict.Remove(key, out V? value))
        {
            return value;
        }

        throw new KeyError(Repr(key));
    }

    public V Pop(K key, V @default)
    {
        if (_dict.Remove(key, out V? value))
        {
            return value;
        }

        return @default;
    }

    public (K, V) PopItem(bool last = false)
    {
        var pair = last ? _dict.Last() : _dict.First();
        _dict.Remove(pair.Key);

        return (pair.Key, pair.Value);
    }

    public V SetDefault(K key, V @default)
    {
        if (_dict.TryGetValue(key, out V? value))
        {
            return value;
        }

        return _dict[key] = @default;
    }

    public void Update(IMapping<K, V> other)
    {
        foreach (var key in other.Keys())
        {
            _dict[key] = other.__GetItem__(key);
        }
    }

    public void Update(IIterable<(K, V)> other)
    {
        foreach (var (key, value) in other)
        {
            _dict[key] = value;
        }
    }

    public IValuesView<V> Values()
    {
        throw new NotImplementedException();
    }

    public bool __Contains__(K key)
    {
        return _dict.ContainsKey(key);
    }

    public void __DelItem__(K key)
    {
        if (!_dict.Remove(key))
        {
            throw new KeyError(Repr(key));
        }
    }

    public V __GetItem__(K key)
    {
        if (_dict.TryGetValue(key, out V? value))
        {
            return value;
        }

        throw new KeyError(Repr(key));
    }

    public Iterator<K> __Iter__()
    {
        return Keys().__Iter__();
    }

    public V this[K key]
    {
        get => __GetItem__(key);
        set => __SetItem__(key, value);
    }

    public uint __Len__()
    {
        return (uint)_dict.Count;
    }

    public override bool __Eq__(Object obj)
    {
        if (obj is Dict<K, V> other)
        {
            return __Eq__(other);
        }

        return false;
    }

    public bool __Eq__(Dict<K, V> other)
    {
        if (other is null)
        {
            return false;
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

            if (!Operator.Exports.Eq(kv.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    public void __SetItem__(K key, V value)
    {
        _dict[key] = value;
    }

    /// <summary>
    /// Delegate to specialized GetEnumerator() for generalized one.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public Dictionary<K, V> ToDictionary()
    {
        return new Dictionary<K, V>(_dict);
    }

    public Dict<K, V> __Or__(Dict<K, V> other)
    {
        var newDict = Copy();
        newDict.Update(other);

        return newDict;
    }

    public void __IOr__(Dict<K, V> other)
    {
        Update(other);
    }

    public IMapping<K, V> __Or__(IMapping<K, V> other)
    {
        var newDict = Copy();
        newDict.Update(other);

        return newDict;
    }

    public void __IOr__(IMapping<K, V> other)
    {
        Update(other);
    }

    public IMapping<K, V> __Or__(IIterable<(K, V)> other)
    {
        var newDict = Copy();
        newDict.Update(other);

        return newDict;
    }

    public void __IOr__(IIterable<(K, V)> other)
    {
        Update(other);
    }

    public static Dict<K, V> operator |(Dict<K, V> left, Dict<K, V> right)
    {
        return left.__Or__(right);
    }

    public static bool operator ==(Dict<K, V> left, Dict<K, V> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.__Eq__(right);
    }

    public static bool operator !=(Dict<K, V> left, Dict<K, V> right)
    {
        return !(left == right);
    }

    public override int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Dict<K, V>).GetHashCode());
        hashCode.Add(_dict.GetHashCode());

        return hashCode.ToHashCode();
    }

    public override string __Repr__()
    {
        var builder = new StringBuilder();
        builder.Append('{');

        uint i = 1;
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

    public override bool __Bool__()
    {
        return _dict.Count > 0;
    }

    public static bool operator true(Dict<K, V> dict)
    {
        return dict?.__Bool__() ?? false;
    }

    public static bool operator false(Dict<K, V> dict)
    {
        return !(dict?.__Bool__() ?? false);
    }

    public bool IsReadOnly
    {
        get
        {
            return false;
        }
    }

    public void Add(K key)
    {
        if (_dict.ContainsKey(key))
        {
            return;
        }

        _dict[key] = default(V);
    }

    bool System.Collections.Generic.ICollection<K>.Remove(K key)
    {
        return _dict.Remove(key);
    }

    public void CopyTo(K[] array, int arrayIndex)
    {
        if (array is null)
        {
            throw new ArgumentNullException("array cannot be null");
        }

        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException("arrayIndex cannot be less than 0");
        }

        if (array.Length - arrayIndex < _dict.Keys.Count)
        {
            throw new ArgumentException("Number of keys is greater than the available space in the array");
        }

        foreach (var key in _dict.Keys)
        {
            array[arrayIndex] = key;
            ++arrayIndex;
        }
    }
}
