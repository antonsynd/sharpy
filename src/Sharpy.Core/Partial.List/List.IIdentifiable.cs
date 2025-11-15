namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public int __Id__()
    {
        return GetHashCode();
    }
}
