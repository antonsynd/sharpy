using System.Collections.Generic;
namespace Sharpy
{
    public sealed partial class ListReverseIterator<T>
    {
        /// <inheritdoc/>
        public override bool MoveNext()
        {
            var count = ((IReadOnlyCollection<T>)_list).Count;
            if (_index < count)
            {
                _current = _list[(int)(count - _index - 1)];
                ++_index;
                return true;
            }

            _current = default;
            return false;
        }
    }
}
