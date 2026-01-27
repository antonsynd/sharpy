namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Determines whether this list equals another list by comparing elements.
    /// </summary>
    public bool Equals(List<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
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

    /// <summary>
    /// Determines whether this list equals another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is List<T> other)
        {
            return Equals(other);
        }

        return false;
    }

    /// <summary>
    /// Deprecated: Use <see cref="Equals(List{T}?)"/> instead.
    /// </summary>
    public bool __Eq__(List<T> other)
    {
        return Equals(other);
    }

    /// <summary>
    /// Deprecated: Use <see cref="Equals(object?)"/> instead.
    /// </summary>
    /// <remarks>
    /// Required for IEquatable interface compatibility.
    /// </remarks>
    public bool __Eq__(object other)
    {
        return Equals(other);
    }
}
