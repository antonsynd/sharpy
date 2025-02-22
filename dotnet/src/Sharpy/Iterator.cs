using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    public abstract class Iterator<T> : Iterable<T>
    {
        public abstract IEnumerator<T> GetEnumerator();

        public abstract Iterator<T> __Iter__();

        /// <remarks>
        /// Type-erased version of GetEnumerator().
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
