using System.Collections.Generic;
namespace Sharpy.Core
{
    public sealed partial class ListIterator<T>
    {
        /// <summary>
        /// Deprecated: Use <see cref="Iterator{T}.Next()"/> instead.
        /// </summary>
        public override T __Next__()
        {
            if (_index < ((IReadOnlyCollection<T>)_list).Count)
            {
                var res = _list[(int)_index];
                ++_index;
                return res;
            }

            throw new StopIteration();
        }
    }
}
