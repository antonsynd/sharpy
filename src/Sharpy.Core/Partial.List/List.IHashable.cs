namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public override int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(List<T>).GetHashCode());
        hashCode.Add(_list.GetHashCode());

        return hashCode.ToHashCode();
    }
}
