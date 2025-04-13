namespace Sharpy;
public sealed partial class List<T>
{
    /// <inheritdoc/>
    public bool Equals(List<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        return __Eq__(other);
    }
}
