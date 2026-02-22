using System.Collections.Generic;
namespace Sharpy
{
    public sealed partial class ListIterator<T>
    {
        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (_index < ((IReadOnlyCollection<T>)_list).Count)
            {
                _current = _list[(int)_index];
                ++_index;
                return true;
            }

            _current = default;
            return false;
        }
    }
}
