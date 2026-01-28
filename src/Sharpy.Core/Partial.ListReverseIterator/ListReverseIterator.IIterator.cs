namespace Sharpy.Core;

public sealed partial class ListReverseIterator<T>
{
    /// <summary>
    /// Deprecated: Use <see cref="Iterator{T}.Next()"/> instead.
    /// </summary>
    public override T __Next__()
    {
        if (_index < _list.Count)
        {
            var res = _list[(int)(_list.Count - _index - 1)];
            ++_index;
            return res;
        }

        throw new StopIteration();
    }
}
