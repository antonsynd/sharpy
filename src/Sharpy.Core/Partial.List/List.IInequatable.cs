namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public bool __Ne__(object other)
    {
        return !__Eq__(other);
    }
}
