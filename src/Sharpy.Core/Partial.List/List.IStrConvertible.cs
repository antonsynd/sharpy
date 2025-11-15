namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public string __Str__()
    {
        return __Repr__();
    }
}
