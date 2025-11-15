namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <inheritdoc/>
    public override bool __Bool__()
    {
        return _set.Count > 0;
    }
}
