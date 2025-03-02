using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static Iterator<T> Iter<T>(Iterable<T> iterable)
        {
            return iterable.__Iter__();
        }
    }
}
