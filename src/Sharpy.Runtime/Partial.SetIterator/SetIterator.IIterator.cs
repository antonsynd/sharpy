namespace Sharpy;

public sealed partial class SetIterator<T>
{
    /// <inheritdoc/>
    public override T __Next__()
    {
        if (_setEnumerator.MoveNext())
        {
            return _setEnumerator.Current;
        }

        throw new StopIteration();
    }
}
