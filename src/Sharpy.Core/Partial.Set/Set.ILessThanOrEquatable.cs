namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <summary>
    /// Deprecated: Use <see cref="IsSubset(Set{T})"/> instead.
    /// </summary>
    public bool __Le__(Set<T> other)
    {
        return IsSubset(other);
    }

    /// <summary>
    /// Deprecated: Use IsSubset instead.
    /// </summary>
    public bool __Le__(Collections.Interfaces.ISet<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var numElems = _set.Count;
        uint otherNumElems = 0;

        if (numElems == otherNumElems)
        {
            return Equals(other);
        }
        else if (numElems > otherNumElems)
        {
            return false;
        }

        foreach (var x in other)
        {
            // TODO: It is possible that the other is implemented
            // incorrectly and has multiple copies of the same element,
            // in which case, we should check if we've seen it before.
            if (_set.Contains(x))
            {
                --numElems;
            }
        }

        return numElems == 0;
    }
}
