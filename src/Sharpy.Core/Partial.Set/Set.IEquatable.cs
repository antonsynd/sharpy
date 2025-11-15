namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public bool __Eq__(object obj)
    {
        if (obj is Set<T> other)
        {
            return __Eq__(other);
        }

        return false;
    }

    /// <inheritdoc/>
    public bool __Eq__(Set<T> other)
    {
        if (other is null)
        {
            return false;
        }

        return _set.SetEquals(other._set);
    }

    /// <inheritdoc/>
    public bool __Eq__(Collections.Interfaces.ISet<T> other)
    {
        uint numElems = 0;

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
}
