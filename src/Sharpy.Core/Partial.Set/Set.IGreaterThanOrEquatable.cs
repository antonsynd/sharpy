namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public bool __Ge__(Set<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        return _set.IsSupersetOf(other._set);
    }

    /// <inheritdoc/>
    public bool __Ge__(Collections.Interfaces.ISet<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        var numElems = _set.Count;
        uint otherNumElems = 0;

        foreach (var x in other)
        {
            ++otherNumElems;

            if (!_set.Contains(x))
            {
                return false;
            }
        }

        return otherNumElems <= numElems;
    }
}
