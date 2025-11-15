namespace Sharpy.Core;

using Collections.Interfaces;

public static partial class Exports
{
    public static Iterator<T> Iter<T>(IIterable<T> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        return iterable.__Iter__();
    }
}
