using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static Iterator<T> Iter<T>(Iterable<T> iterable) where T : notnull
        {
            return iterable.__Iter__();
        }
    }
}
