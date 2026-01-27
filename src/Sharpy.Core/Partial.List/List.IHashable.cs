namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <summary>
    /// Returns a hash code for this list.
    /// </summary>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(List<T>).GetHashCode());
        hashCode.Add(_list.GetHashCode());

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Deprecated: Use <see cref="GetHashCode()"/> instead.
    /// </summary>
    public int __Hash__()
    {
        return GetHashCode();
    }
}
