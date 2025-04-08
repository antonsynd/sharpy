using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class __Exports
    {
        public static Iterator<T> Reversed<T>(IReversible<T> reversible)
        {
            if (reversible is null)
            {
                throw new TypeError("Reversed() reversible argument cannot be None");
            }

            return reversible.__Reversed__();
        }
    }
}
