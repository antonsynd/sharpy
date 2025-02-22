using System.Collections.Generic;

namespace Sharpy
{
    public interface Iterable<T> : IEnumerable<T>
    {
        Iterator<T> __Iter__();
    }
}
