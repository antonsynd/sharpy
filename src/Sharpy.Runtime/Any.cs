namespace Sharpy;

using Collections.Interfaces;

public static partial class Exports
{
    /// <summary>
    /// Return True if any element of the iterable is true. If the iterable is empty, return False.
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable</typeparam>
    /// <param name="iterable">The iterable to check</param>
    /// <returns>True if any element is truthy, False otherwise</returns>
    public static bool Any<T>(IIterable<T> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("any", "iterable");
        }

        foreach (var item in iterable)
        {
            if (Bool(item))
            {
                return true;
            }
        }

        return false;
    }
}
