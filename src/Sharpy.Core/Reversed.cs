namespace Sharpy.Core;

using Collections.Interfaces;

public static partial class Exports
{
    public static Iterator<T> Reversed<T>(IReversible<T> reversible)
    {
        if (reversible is null)
        {
            throw TypeError.ArgNone("reversed", "reversible");
        }

        return reversible.__Reversed__();
    }
}
