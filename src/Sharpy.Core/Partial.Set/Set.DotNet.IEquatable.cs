namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public bool Equals(Set<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        return __Eq__(other);
    }

    /// <inheritdoc/>
    public bool Equals(Collections.Interfaces.ISet<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        return __Eq__(other);
    }
}
