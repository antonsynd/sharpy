namespace Sharpy;

using Collections.Interfaces;

public static partial class Exports
{
    /// <summary>
    /// Return the length (the number of items) of an object. The argument
    /// may be a sequence (such as a string, bytes, tuple, list, or range)
    /// or a collection (such as a dictionary, set, or frozen set).
    /// </summary>
    public static uint Len(ISized sized)
    {
        if (sized is null)
        {
            throw TypeError.ArgNone("len", "sized");
        }

        return sized.__Len__();
    }

    public static uint Len(string s)
    {
        return (uint)s.Length;
    }
}
