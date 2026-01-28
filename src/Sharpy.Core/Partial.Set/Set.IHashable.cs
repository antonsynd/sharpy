namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <summary>
    /// Returns a hash code for this set.
    /// </summary>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Set<T>).GetHashCode());
        hashCode.Add(_set.GetHashCode());

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Deprecated: Use <see cref="GetHashCode()"/> instead.
    /// </summary>
    public int __Hash__() => GetHashCode();
}
