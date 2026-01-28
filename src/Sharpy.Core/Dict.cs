namespace Sharpy.Core;

using System.Collections;
using System.Text;

using Collections.Interfaces;
using Operator;
using static Sharpy.Core.Exports;

public sealed partial class Dict<K, V>
    : Object,
      IDictionary<K, V>,
      IReadOnlyDictionary<K, V>,
      IMutableMapping<K, V>
    where K : notnull
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
        // Delegate to __Iter__() to ensure consistent iteration behavior
        return __Iter__();
    }

    public IItemsView<K, V> Items()
    {
        return new DictItemsView<K, V>(_dict);
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
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }

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
        return new DictValuesView<K, V>(_dict.Values);
    }

    public bool __Contains__(K key)
    {
        try
        {
            return _dict.ContainsKey(key);
        }
        catch (NullReferenceException)
        {
            throw new ArgumentNullException(nameof(key));
        }
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

    public int __Len__()
    {
        return _dict.Count;
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

    #region IDictionary<K,V> and IReadOnlyDictionary<K,V> implementation

    /// <summary>
    /// Gets the number of key/value pairs in the dictionary.
    /// </summary>
    public int Count => _dict.Count;

    /// <summary>
    /// Gets a collection containing the keys in the dictionary.
    /// </summary>
    System.Collections.Generic.ICollection<K> System.Collections.Generic.IDictionary<K, V>.Keys => _dict.Keys;

    /// <summary>
    /// Gets a collection containing the values in the dictionary.
    /// </summary>
    System.Collections.Generic.ICollection<V> System.Collections.Generic.IDictionary<K, V>.Values => _dict.Values;

    /// <summary>
    /// Gets an enumerable containing the keys in the dictionary.
    /// </summary>
    IEnumerable<K> System.Collections.Generic.IReadOnlyDictionary<K, V>.Keys => _dict.Keys;

    /// <summary>
    /// Gets an enumerable containing the values in the dictionary.
    /// </summary>
    IEnumerable<V> System.Collections.Generic.IReadOnlyDictionary<K, V>.Values => _dict.Values;

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    public bool ContainsKey(K key) => _dict.ContainsKey(key);

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    public bool TryGetValue(K key, out V value) => _dict.TryGetValue(key, out value!);

    /// <summary>
    /// Adds the specified key and value to the dictionary.
    /// </summary>
    /// <exception cref="ArgumentException">An element with the same key already exists.</exception>
    void System.Collections.Generic.IDictionary<K, V>.Add(K key, V value)
    {
        if (_dict.ContainsKey(key))
        {
            throw new ArgumentException($"An element with the key '{key}' already exists.");
        }
        _dict.Add(key, value);
    }

    /// <summary>
    /// Removes the value with the specified key from the dictionary.
    /// </summary>
    /// <returns>true if the element was removed; false if key was not found.</returns>
    bool System.Collections.Generic.IDictionary<K, V>.Remove(K key) => _dict.Remove(key);

    #endregion

    #region ICollection<KeyValuePair<K,V>> implementation

    /// <summary>
    /// Adds a key/value pair to the dictionary.
    /// </summary>
    void System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Add(KeyValuePair<K, V> item)
    {
        ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).Add(item);
    }

    /// <summary>
    /// Determines whether the dictionary contains a specific key/value pair.
    /// </summary>
    bool System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Contains(KeyValuePair<K, V> item)
    {
        return ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).Contains(item);
    }

    /// <summary>
    /// Copies the elements of the dictionary to an array.
    /// </summary>
    void System.Collections.Generic.ICollection<KeyValuePair<K, V>>.CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
    {
        ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Removes a specific key/value pair from the dictionary.
    /// </summary>
    bool System.Collections.Generic.ICollection<KeyValuePair<K, V>>.Remove(KeyValuePair<K, V> item)
    {
        return ((System.Collections.Generic.ICollection<KeyValuePair<K, V>>)_dict).Remove(item);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the key/value pairs.
    /// </summary>
    IEnumerator<KeyValuePair<K, V>> System.Collections.Generic.IEnumerable<KeyValuePair<K, V>>.GetEnumerator()
    {
        return _dict.GetEnumerator();
    }

    #endregion
}
