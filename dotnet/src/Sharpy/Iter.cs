using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class __Exports
    {
        public static Iterator<T> Iter<T>(IIterable<T> iterable)
        {
            if (iterable is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            return iterable.__Iter__();
        }
    }
}
