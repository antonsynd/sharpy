using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static Iterator<T> Reversed<T>(Reversible<T> r) where T : notnull
        {
            return r.__Reversed__();
        }
    }
}
