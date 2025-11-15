namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Set<T>).GetHashCode());
        hashCode.Add(_set.GetHashCode());

        return hashCode.ToHashCode();
    }
}
