namespace Sharpy.Core;

using Collections.Interfaces;

public static partial class Exports
{
    /// <summary>
    /// Return an iterator object from a Sharpy iterable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable.</typeparam>
    /// <param name="iterable">The Sharpy iterable to get an iterator from.</param>
    /// <returns>An iterator for the iterable.</returns>
    /// <exception cref="TypeError">Thrown when iterable is null.</exception>
    public static Iterator<T> Iter<T>(IIterable<T> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        return iterable.__Iter__();
    }

    /// <summary>
    /// Return an iterator object from any C# enumerable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
    /// <param name="enumerable">The C# enumerable to get an iterator from.</param>
    /// <returns>An iterator for the enumerable.</returns>
    /// <exception cref="TypeError">Thrown when enumerable is null.</exception>
    /// <remarks>
    /// If the enumerable is already an IIterable, this method uses its native __Iter__() method
    /// to avoid unnecessary wrapping. Otherwise, it wraps the enumerator using EnumeratorIterator.
    /// This allows any C# IEnumerable to work seamlessly with Sharpy's iterator protocol.
    /// </remarks>
    public static Iterator<T> Iter<T>(IEnumerable<T> enumerable)
    {
        if (enumerable is null)
        {
            throw TypeError.ArgNone("iter", "enumerable");
        }

        // If it's already an IIterable, use its native __Iter__
        if (enumerable is IIterable<T> iterable)
        {
            return iterable.__Iter__();
        }

        // For pure C# IEnumerable, use EnumeratorIterator directly
        return new EnumeratorIterator<T>(enumerable.GetEnumerator());
    }
}
