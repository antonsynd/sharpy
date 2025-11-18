namespace Sharpy.Core;

using Collections.Interfaces;

/// <summary>
/// Extension methods to bridge C# IEnumerable with Sharpy's iterator protocol.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Converts any C# IEnumerable to a Sharpy Iterator.
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
    /// <param name="enumerable">The C# enumerable to convert.</param>
    /// <returns>A Sharpy Iterator that wraps the enumerable.</returns>
    /// <exception cref="TypeError">Thrown when enumerable is null.</exception>
    /// <remarks>
    /// If the enumerable is already an IIterable, this method uses its native __Iter__() method.
    /// Otherwise, it wraps the enumerator using EnumeratorIterator.
    /// </remarks>
    public static Iterator<T> ToIterator<T>(this IEnumerable<T> enumerable)
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

        // Otherwise wrap the enumerator
        return new EnumeratorIterator<T>(enumerable.GetEnumerator());
    }

    /// <summary>
    /// Allows C# IEnumerable to be used as Sharpy IIterable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
    /// <param name="enumerable">The C# enumerable to adapt.</param>
    /// <returns>An IIterable wrapper around the enumerable.</returns>
    /// <exception cref="ArgumentNullException">Thrown when enumerable is null.</exception>
    /// <remarks>
    /// If the enumerable is already an IIterable, this method returns it directly.
    /// Otherwise, it wraps it in an adapter that implements IIterable.
    /// </remarks>
    public static IIterable<T> AsIterable<T>(this IEnumerable<T> enumerable)
    {
        if (enumerable is null)
        {
            throw new ArgumentNullException(nameof(enumerable));
        }

        if (enumerable is IIterable<T> iterable)
        {
            return iterable;
        }

        return new EnumerableAdapter<T>(enumerable);
    }
}

/// <summary>
/// Adapter to make C# IEnumerable implement Sharpy IIterable.
/// </summary>
/// <typeparam name="T">The type of elements in the enumerable.</typeparam>
internal sealed class EnumerableAdapter<T> : IIterable<T>
{
    private readonly IEnumerable<T> _enumerable;

    /// <summary>
    /// Initializes a new instance of the EnumerableAdapter class.
    /// </summary>
    /// <param name="enumerable">The C# enumerable to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when enumerable is null.</exception>
    public EnumerableAdapter(IEnumerable<T> enumerable)
    {
        _enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
    }

    /// <inheritdoc/>
    public Iterator<T> __Iter__()
    {
        return new EnumeratorIterator<T>(_enumerable.GetEnumerator());
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _enumerable.GetEnumerator();

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => GetEnumerator();
}
