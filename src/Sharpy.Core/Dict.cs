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

    /// <summary>
    /// Deprecated: Use <see cref="ContainsKey(K)"/> instead.
    /// </summary>
    public bool Contains(K key) => ContainsKey(key);

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

    /// <summary>
    /// Returns an enumerator that iterates through the keys.
    /// </summary>
    public IEnumerator<K> GetEnumerator()
    {
        return _dict.Keys.GetEnumerator();
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

    /// <summary>
    /// Deprecated: Use <see cref="ContainsKey(K)"/> instead.
    /// </summary>
    public bool __Contains__(K key) => ContainsKey(key);

    /// <summary>
    /// Removes the item with the specified key from the dictionary.
    /// </summary>
    /// <exception cref="KeyError">Thrown if the key does not exist.</exception>
    public void Remove(K key)
    {
        if (!_dict.Remove(key))
        {
            throw new KeyError(Repr(key));
        }
    }

    /// <summary>
    /// Deprecated: Use <see cref="Remove(K)"/> instead.
    /// </summary>
    public void __DelItem__(K key) => Remove(key);

    /// <summary>
    /// Deprecated: Use the indexer <c>dict[key]</c> instead.
    /// </summary>
    public V __GetItem__(K key) => this[key];

    /// <summary>
    /// Deprecated: Use <see cref="GetEnumerator()"/> instead.
    /// </summary>
    public Iterator<K> __Iter__()
    {
        return Keys().__Iter__();
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
    /// Deprecated: Use <see cref="Count"/> instead.
    /// </summary>
    public int __Len__() => Count;

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

            if (!Operator.Exports.Eq(kv.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Deprecated: Use <see cref="Equals(Dict{K,V}?)"/> instead.
    /// </summary>
    public bool __Eq__(Dict<K, V> other) => Equals(other);

    /// <summary>
    /// Required for Object base class compatibility. Delegates to <see cref="Equals(Dict{K,V}?)"/>.
    /// Will become <c>override Equals(object?)</c> when Object base class is removed.
    /// </summary>
    public override bool __Eq__(object other)
    {
        if (other is Dict<K, V> dict)
        {
            return Equals(dict);
        }

        return false;
    }

    /// <summary>
    /// Deprecated: Use the indexer <c>dict[key] = value</c> instead.
    /// </summary>
    public void __SetItem__(K key, V value) => this[key] = value;

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

    public static bool operator ==(Dict<K, V>? left, Dict<K, V>? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(Dict<K, V> left, Dict<K, V> right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns a hash code for this dictionary.
    /// </summary>
    /// <remarks>
    /// While Dict inherits from Object, GetHashCode() is sealed and delegates to __Hash__().
    /// Once Dict no longer inherits from Object, this will become:
    /// <code>public override int GetHashCode()</code>
    /// </remarks>
    public override int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Dict<K, V>).GetHashCode());
        hashCode.Add(_dict.GetHashCode());

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Returns a string representation of this dictionary.
    /// </summary>
    /// <remarks>
    /// While Dict inherits from Object, ToString() is sealed and delegates to __Str__(),
    /// which by default calls __Repr__(). Once Dict no longer inherits from Object,
    /// this will become:
    /// <code>public override string ToString()</code>
    /// </remarks>
    public override string __Repr__()
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

    /// <summary>
    /// Deprecated: Use <c>dict</c> in a boolean context (operator true/false) instead.
    /// </summary>
    public override bool __Bool__()
    {
        return _dict.Count > 0;
    }

    public static bool operator true(Dict<K, V>? dict)
    {
        return dict is not null && dict._dict.Count > 0;
    }

    public static bool operator false(Dict<K, V>? dict)
    {
        return dict is null || dict._dict.Count == 0;
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
