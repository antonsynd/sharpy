using System.Collections.Generic;
namespace Sharpy.Core
{
    public sealed partial class ListReverseIterator<T>
    {
        /// <summary>
        /// Deprecated: Use <see cref="Iterator{T}.Next()"/> instead.
        /// </summary>
        public override T __Next__()
        {
            var count = ((IReadOnlyCollection<T>)_list).Count;
            if (_index < count)
            {
                var res = _list[(int)(count - _index - 1)];
                ++_index;
                return res;
            }

            throw new StopIteration();
        }
    }
}
