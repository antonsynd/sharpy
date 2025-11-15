namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public bool __Lt__(Set<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        return _set.IsProperSubsetOf(other._set);
    }

    /// <inheritdoc/>
    public bool __Lt__(Collections.Interfaces.ISet<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var numElems = _set.Count;
        uint otherNumElems = 0;

        if (numElems >= otherNumElems)
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
