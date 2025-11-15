namespace Sharpy.Core;

using Collections.Interfaces;

/// <summary>
/// Iterator that yields elements from an iterable for which a predicate is true.
/// </summary>
/// <typeparam name="T">The type of elements in the iterable</typeparam>
public class FilterIterator<T> : Iterator<T>
{
    private readonly Iterator<T> _iterator;
    private readonly Func<T, bool> _predicate;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterIterator{T}"/> class.
    /// </summary>
    /// <param name="predicate">The predicate function</param>
    /// <param name="iterable">The iterable to filter</param>
    public FilterIterator(Func<T, bool> predicate, IIterable<T> iterable)
    {
        if (predicate is null)
        {
            throw TypeError.ArgNone("filter", "predicate");
        }

        if (iterable is null)
        {
            throw TypeError.ArgNone("filter", "iterable");
        }

        _predicate = predicate;
        _iterator = iterable.__Iter__();
    }

    /// <inheritdoc/>
    public override T __Next__()
    {
        while (true)
        {
            var value = _iterator.__Next__();
            if (_predicate(value))
            {
                return value;
            }
        }
    }
}

public static partial class Exports
{
    /// <summary>
    /// Construct an iterator from those elements of iterable for which predicate is true.
    /// If predicate is None, return the elements that are true.
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable</typeparam>
    /// <param name="predicate">The predicate function to test each element</param>
    /// <param name="iterable">The iterable to filter</param>
    /// <returns>A filter iterator</returns>
    public static FilterIterator<T> Filter<T>(Func<T, bool> predicate, IIterable<T> iterable)
    {
        return new FilterIterator<T>(predicate, iterable);
    }
}
