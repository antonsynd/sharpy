namespace Sharpy;

using Collections.Interfaces;

public static partial class Exports
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
