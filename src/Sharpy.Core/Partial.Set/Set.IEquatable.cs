namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <summary>
    /// Determines whether this set equals another set by comparing elements.
    /// </summary>
    public bool Equals(Set<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _set.SetEquals(other._set);
    }

    /// <summary>
    /// Deprecated: Use <see cref="Equals(Set{T}?)"/> instead.
    /// </summary>
    public bool __Eq__(Set<T> other)
    {
        return Equals(other);
    }

    /// <summary>
    /// Deprecated: Use <see cref="Equals(Set{T}?)"/> instead.
    /// </summary>
    /// <remarks>
    /// Required for IEquatable interface compatibility. Overrides Object.__Eq__(object)
    /// to provide value equality for Set.
    /// </remarks>
    public override bool __Eq__(object other)
    {
        if (other is Set<T> set)
        {
            return Equals(set);
        }

        return false;
    }

    /// <summary>
    /// Determines whether this set equals another ISet by comparing elements.
    /// </summary>
    /// <remarks>
    /// Required by IEquatable&lt;ISet&lt;T&gt;&gt; interface.
    /// </remarks>
    public bool Equals(Collections.Interfaces.ISet<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Count elements and check containment
        int numElems = 0;

        foreach (var x in other)
        {
            if (!_set.Contains(x))
            {
                return false;
            }

            ++numElems;
        }

        return numElems == _set.Count;
    }

    /// <summary>
    /// Deprecated: Use <see cref="Equals(Collections.Interfaces.ISet{T}?)"/> instead.
    /// </summary>
    public bool __Eq__(Collections.Interfaces.ISet<T> other)
    {
        return Equals(other);
    }
}
