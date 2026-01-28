namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <summary>
    /// Returns whether this set has no elements in common with other.
    /// </summary>
    public bool IsDisjoint(Set<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        return !_set.Overlaps(other._set);
    }

    /// <summary>
    /// Deprecated: Use <see cref="Intersection(Set{T})"/> instead.
    /// </summary>
    public Set<T> __And__(Set<T> other)
    {
        return Intersection(other);
    }

    /// <summary>
    /// Deprecated: Use <see cref="Union(Set{T})"/> instead.
    /// </summary>
    public Set<T> __Or__(Set<T> other)
    {
        return Union(other);
    }

    /// <summary>
    /// Returns union with other set (reverse operand order for insertion).
    /// </summary>
    /// <remarks>
    /// For insertion order, the other set is iterated through first.
    /// Deprecated: Use <see cref="Union(Set{T})"/> with operands swapped instead.
    /// </remarks>
    public Set<T> __ROr__(Set<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var result = new Set<T>(other._set);

        foreach (var item in _set)
        {
            result._set.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Returns difference with other as left operand.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <c>other.Difference(this)</c> instead.
    /// </remarks>
    public Set<T> __RSub__(Set<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var result = new Set<T>();

        foreach (var item in other._set)
        {
            if (!_set.Contains(item))
            {
                result._set.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Deprecated: Use <see cref="Difference(Set{T})"/> instead.
    /// </summary>
    public Set<T> __Sub__(Set<T> other)
    {
        return Difference(other);
    }

    /// <summary>
    /// Deprecated: Use <see cref="SymmetricDifference(Set{T})"/> instead.
    /// </summary>
    public Set<T> __XOr__(Set<T> other)
    {
        return SymmetricDifference(other);
    }

    /// <summary>
    /// Deprecated: Use <see cref="Intersection(Set{T})"/> instead.
    /// </summary>
    public Collections.Interfaces.ISet<T> __And__(Collections.Interfaces.ISet<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var result = new Set<T>();

        foreach (var item in _set)
        {
            if (other.__Contains__(item))
            {
                result._set.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Deprecated: Use <see cref="Union(Set{T})"/> instead.
    /// </summary>
    public Collections.Interfaces.ISet<T> __Or__(Collections.Interfaces.ISet<T> other)
    {
        var result = new Set<T>(this);

        foreach (var item in other)
        {
            result._set.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Deprecated: Use <see cref="Difference(Set{T})"/> instead.
    /// </summary>
    public Collections.Interfaces.ISet<T> __Sub__(Collections.Interfaces.ISet<T> other)
    {
        var result = new Set<T>();

        foreach (var elem in _set)
        {
            if (other.__Contains__(elem))
            {
                continue;
            }

            result._set.Add(elem);
        }

        return result;
    }

    /// <summary>
    /// Returns difference with other as left operand.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <c>other.Difference(this)</c> instead.
    /// </remarks>
    public Collections.Interfaces.ISet<T> __RSub__(Collections.Interfaces.ISet<T> other)
    {
        var result = new Set<T>();

        foreach (var elem in other)
        {
            if (_set.Contains(elem))
            {
                continue;
            }

            result._set.Add(elem);
        }

        return result;
    }

    /// <summary>
    /// Deprecated: Use <see cref="SymmetricDifference(Set{T})"/> instead.
    /// </summary>
    public Collections.Interfaces.ISet<T> __XOr__(Collections.Interfaces.ISet<T> other)
    {
        var result = new Set<T>();

        foreach (var elem in _set)
        {
            if (other.__Contains__(elem))
            {
                continue;
            }

            result._set.Add(elem);
        }

        foreach (var elem in other)
        {
            if (_set.Contains(elem))
            {
                continue;
            }

            result._set.Add(elem);
        }

        return result;
    }

    /// <summary>
    /// Returns union with other set (reverse operand order for insertion).
    /// </summary>
    /// <remarks>
    /// Deprecated: Use <see cref="Union(Set{T})"/> with operands swapped instead.
    /// </remarks>
    public Collections.Interfaces.ISet<T> __ROr__(Collections.Interfaces.ISet<T> other)
    {
        var result = new Set<T>(other);
        result._set.UnionWith(_set);

        return result;
    }

    /// <summary>
    /// Returns whether this set has no elements in common with other.
    /// </summary>
    public bool IsDisjoint(Collections.Interfaces.ISet<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        foreach (var item in other)
        {
            if (_set.Contains(item))
            {
                return false;
            }
        }

        return true;
    }
}
