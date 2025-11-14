namespace Sharpy;

using Collections.Interfaces;

public static partial class Exports
{
    /// <summary>
    /// Return True if all elements of the iterable are true (or if the iterable is empty).
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable</typeparam>
    /// <param name="iterable">The iterable to check</param>
    /// <returns>True if all elements are truthy, False otherwise</returns>
    public static bool All<T>(IIterable<T> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("all", "iterable");
        }

        foreach (var item in iterable)
        {
            // Check truthiness - for bool, check directly; for others use Operator.Truth
            if (item is bool b)
            {
                if (!b)
                {
                    return false;
                }
            }
            else if (item is IBoolConvertible convertible)
            {
                if (!Operator.Exports.Truth(convertible))
                {
                    return false;
                }
            }
            else if (item is null || (item is int i && i == 0) || (item is string s && s.Length == 0))
            {
                return false;
            }
        }

        return true;
    }
}
