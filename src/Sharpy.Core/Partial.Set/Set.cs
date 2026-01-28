namespace Sharpy.Core;

using Collections.Interfaces;

public sealed partial class Set<T>
    : Object,
      System.Collections.Generic.ISet<T>,
      IMutableSet<Set<T>, T>,
      ILessThanOrEquatable<Set<T>>, IGreaterThanOrEquatable<Set<T>>
{
    // Internal for SetIterator access to avoid infinite recursion when GetEnumerator delegates to __Iter__
    internal readonly HashSet<T> _set;

    public Set()
    {
        _set = [];
    }

    public Set(Set<T> set) : this()
    {
        _set.UnionWith(set._set);
    }

    public Set(IEnumerable<T> enumerable) : this()
    {
        if (enumerable is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        foreach (var x in enumerable)
        {
            _set.Add(x);
        }
    }

    public Set<T> Copy()
    {
        var newSet = new Set<T>();
        newSet._set.EnsureCapacity(_set.Count);
        newSet._set.UnionWith(_set);

        return newSet;
    }

    public bool IsSubset(Set<T> other)
    {
        return __Le__(other);
    }

    public bool IsSuperset(Set<T> other)
    {
        return __Ge__(other);
    }

    /// <summary>
    /// Returns a new set with elements from both sets.
    /// </summary>
    public Set<T> Union(Set<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var result = new Set<T>(_set);

        foreach (var item in other._set)
        {
            result._set.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Returns a new set with elements common to both sets.
    /// </summary>
    public Set<T> Intersection(Set<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var result = new Set<T>();

        foreach (var item in _set)
        {
            if (other._set.Contains(item))
            {
                result._set.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns a new set with elements in this set but not in other.
    /// </summary>
    public Set<T> Difference(Set<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var result = new Set<T>();

        foreach (var item in _set)
        {
            if (!other._set.Contains(item))
            {
                result._set.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns a new set with elements in either set but not both.
    /// </summary>
    public Set<T> SymmetricDifference(Set<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var result = new Set<T>();

        foreach (var item in _set)
        {
            if (!other._set.Contains(item))
            {
                result._set.Add(item);
            }
        }

        foreach (var item in other._set)
        {
            if (!_set.Contains(item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    public HashSet<T> ToHashSet()
    {
        var result = new HashSet<T>();
        result.EnsureCapacity(_set.Count);
        result.UnionWith(_set);

        return result;
    }
}
