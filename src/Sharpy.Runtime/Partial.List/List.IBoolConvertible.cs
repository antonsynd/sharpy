namespace Sharpy;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public override bool __Bool__()
    {
        return _list.Count > 0;
    }
}
