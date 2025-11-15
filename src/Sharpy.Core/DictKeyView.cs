namespace Sharpy.Core;

using System.Collections;
using Collections.Interfaces;

public sealed partial class DictKeyView<K, V> : IKeysView<K> where K : notnull
{
    private readonly Dictionary<K, V>.KeyCollection _keys;

    internal DictKeyView(Dictionary<K, V>.KeyCollection keys)
    {
        _keys = keys;
    }

    public int CompareTo(Set<K>? other)
    {
        throw new NotImplementedException();
    }

    public bool Contains(K x)
    {
        return __Contains__(x);
    }

    public bool Equals(Set<K>? other)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<K> GetEnumerator()
    {
        foreach (var key in _keys)
        {
            yield return key;
        }
    }

    public bool IsDisjoint(Set<K> other)
    {
        throw new NotImplementedException();
    }

    public Set<K> __And__(Set<K> other)
    {
        throw new NotImplementedException();
    }

    public bool __Contains__(K x)
    {
        return _keys.Contains(x);
    }

    public bool __Eq__(Set<K> other)
    {
        throw new NotImplementedException();
    }

    public bool __Ge__(Set<K> other)
    {
        return __Eq__(other) || !__Lt__(other);
    }

    public bool __Gt__(Set<K> other)
    {
        return !__Eq__(other) && !__Lt__(other);
    }

    public Iterator<K> __Iter__()
    {
        throw new NotImplementedException();
    }

    public uint __Len__()
    {
        return (uint)_keys.Count;
    }

    public bool __Le__(Set<K> other)
    {
        throw new NotImplementedException();
    }

    public bool __Lt__(Set<K> other)
    {
        throw new NotImplementedException();
    }

    public bool __Ne__(Set<K> other)
    {
        return !__Eq__(other);
    }

    public Set<K> __Or__(Set<K> other)
    {
        throw new NotImplementedException();
    }

    public static DictKeyView<K, V> operator |(DictKeyView<K, V> left, DictKeyView<K, V> right)
    {
        throw new NotImplementedException();
    }

    public Set<K> __ROr__(Set<K> other)
    {
        throw new NotImplementedException();
    }

    public Set<K> __RSub__(Set<K> other)
    {
        throw new NotImplementedException();
    }

    public Set<K> __Sub__(Set<K> other)
    {
        throw new NotImplementedException();
    }

    public Set<K> __XOr__(Set<K> other)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
