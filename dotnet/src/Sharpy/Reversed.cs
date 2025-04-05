using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static Iterator<T> Reversed<T>(Reversible<T> reversible)
        {
            if (reversible is null)
            {
                throw new TypeError("Reversed() reversible argument cannot be None");
            }

            return reversible.__Reversed__();
        }
    }
}
