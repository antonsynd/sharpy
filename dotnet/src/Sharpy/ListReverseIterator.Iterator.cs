
namespace Sharpy
{
    public sealed partial class ListReverseIterator<T>
    {
        public override T __Next__()
        {
            if (_index < _list.__Len__()) {

                var res = _list[(int)(_list.__Len__() - _index - 1)];

                ++_index;

                return res;
            }

            throw new StopIteration("");
        }
    }
}
