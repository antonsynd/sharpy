namespace Sharpy.Core;

public sealed partial class ListIterator<T>
{
    /// <inheritdoc/>
    public override T __Next__()
    {
        if (_index < _list.__Len__())
        {

            var res = _list[(int)_index];

            ++_index;

            return res;
        }

        throw new StopIteration();
    }
}
