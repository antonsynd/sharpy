using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static Iterator<T> Iter<T>(Iterable<T> iterable)
        {
            if (iterable is null) {
                throw new TypeError("'NoneType' object is not iterable");
            }

            return iterable.__Iter__();
        }
    }
}
