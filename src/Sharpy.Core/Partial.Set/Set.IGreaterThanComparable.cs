namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <summary>
    /// Deprecated: Use <see cref="IsProperSuperset(Set{T})"/> instead.
    /// </summary>
    public bool __Gt__(Set<T> other)
    {
        return IsProperSuperset(other);
    }

    /// <summary>
    /// Deprecated: Use IsProperSuperset instead.
    /// </summary>
    public bool __Gt__(Collections.Interfaces.ISet<T> other)
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

        return otherNumElems < numElems;
    }
}
