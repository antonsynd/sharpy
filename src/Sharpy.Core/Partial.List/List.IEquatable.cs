namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public override bool __Eq__(Object obj)
    {
        if (obj is List<T> other)
        {
            return __Eq__(other);
        }

        return false;
    }

    /// <inheritdoc/>
    public bool __Eq__(List<T> other)
    {
        if (other is null)
        {
            return false;
        }

        if (_list.Count != other._list.Count)
        {
            return false;
        }

        var comparer = System.Collections.Generic.EqualityComparer<T>.Default;

        for (int i = 0; i < _list.Count; ++i)
        {
            var leftElem = _list[i];
            var rightElem = other._list[i];

            if (!comparer.Equals(leftElem, rightElem))
            {
                return false;
            }
        }

        return true;
    }
}
