namespace Sharpy.Core;

using System.Collections;
using Collections.Interfaces;

/// <summary>
/// View of dictionary keys as a set-like object.
/// This view reflects changes to the underlying dictionary.
/// Supports set operations like intersection, union, difference.
/// </summary>
public sealed partial class DictKeyView<K, V> : IKeysView<K> where K : notnull
{
    private readonly Dictionary<K, V>.KeyCollection _keys;

    internal DictKeyView(Dictionary<K, V>.KeyCollection keys)
    {
        _keys = keys;
    }

    public int CompareTo(Set<K>? other)
    {
        if (other == null)
            return 1;

        var thisCount = __Len__();
        var otherCount = other.__Len__();

        if (thisCount < otherCount)
            return -1;
        if (thisCount > otherCount)
            return 1;
        return 0;
    }

    public bool Contains(K x)
    {
        return __Contains__(x);
    }

    public bool Equals(Set<K>? other)
    {
        return __Eq__(other);
    }

    public IEnumerator<K> GetEnumerator()
    {
        foreach (var key in _keys)
        {
            yield return key;
        }
    }

    /// <summary>
    /// Return True if the view and other have a null intersection (no common elements).
    /// </summary>
    public bool IsDisjoint(Set<K> other)
    {
        foreach (var key in _keys)
        {
            if (other.__Contains__(key))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Return intersection with another set.
    /// </summary>
    public Set<K> __And__(Set<K> other)
    {
        var result = new Set<K>();
        foreach (var key in _keys)
        {
            if (other.__Contains__(key))
            {
                result.Add(key);
            }
        }
        return result;
    }

    public bool __Contains__(K x)
    {
        return _keys.Contains(x);
    }

    /// <summary>
    /// Check equality with another set (same elements).
    /// </summary>
    public bool __Eq__(Set<K> other)
    {
        if (__Len__() != other.__Len__())
        {
            return false;
        }

        foreach (var key in _keys)
        {
            if (!other.__Contains__(key))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Check if this is a superset or equal to other.
    /// </summary>
    public bool __Ge__(Set<K> other)
    {
        return __Eq__(other) || __Gt__(other);
    }

    /// <summary>
    /// Check if this is a proper superset of other.
    /// </summary>
    public bool __Gt__(Set<K> other)
    {
        if (__Len__() <= other.__Len__())
        {
            return false;
        }

        // Check if all elements of other are in this
        foreach (var item in other)
        {
            if (!__Contains__(item))
            {
                return false;
            }
        }
        return true;
    }

    public Iterator<K> __Iter__()
    {
        return new EnumeratorIterator<K>(GetEnumerator());
    }

    public uint __Len__()
    {
        return (uint)_keys.Count;
    }

    /// <summary>
    /// Check if this is a subset or equal to other.
    /// </summary>
    public bool __Le__(Set<K> other)
    {
        if (__Len__() > other.__Len__())
        {
            return false;
        }

        foreach (var key in _keys)
        {
            if (!other.__Contains__(key))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Check if this is a proper subset of other.
    /// </summary>
    public bool __Lt__(Set<K> other)
    {
        if (__Len__() >= other.__Len__())
        {
            return false;
        }

        foreach (var key in _keys)
        {
            if (!other.__Contains__(key))
            {
                return false;
            }
        }
        return true;
    }

    public bool __Ne__(Set<K> other)
    {
        return !__Eq__(other);
    }

    /// <summary>
    /// Return union with another set.
    /// </summary>
    public Set<K> __Or__(Set<K> other)
    {
        var result = new Set<K>();
        foreach (var key in _keys)
        {
            result.Add(key);
        }
        foreach (var item in other)
        {
            result.Add(item);
        }
        return result;
    }

    public static DictKeyView<K, V> operator |(DictKeyView<K, V> left, DictKeyView<K, V> right)
    {
        throw new NotSupportedException("Cannot create a DictKeyView from union operation. Use __Or__ to get a Set instead.");
    }

    /// <summary>
    /// Right-side union (when dict view is on the right).
    /// </summary>
    public Set<K> __ROr__(Set<K> other)
    {
        return __Or__(other);
    }

    /// <summary>
    /// Right-side difference (when dict view is on the right: other - this).
    /// </summary>
    public Set<K> __RSub__(Set<K> other)
    {
        var result = new Set<K>();
        foreach (var item in other)
        {
            if (!__Contains__(item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>
    /// Return difference (elements in this but not in other).
    /// </summary>
    public Set<K> __Sub__(Set<K> other)
    {
        var result = new Set<K>();
        foreach (var key in _keys)
        {
            if (!other.__Contains__(key))
            {
                result.Add(key);
            }
        }
        return result;
    }

    /// <summary>
    /// Return symmetric difference (elements in either but not both).
    /// </summary>
    public Set<K> __XOr__(Set<K> other)
    {
        var result = new Set<K>();

        // Add elements from this that are not in other
        foreach (var key in _keys)
        {
            if (!other.__Contains__(key))
            {
                result.Add(key);
            }
        }

        // Add elements from other that are not in this
        foreach (var item in other)
        {
            if (!__Contains__(item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
