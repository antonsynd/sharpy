namespace Sharpy.Core;

/// <summary>
/// Iterator that yields tuples containing elements from two iterables.
/// </summary>
/// <typeparam name="T1">The type of elements in the first iterable</typeparam>
/// <typeparam name="T2">The type of elements in the second iterable</typeparam>
public class ZipIterator<T1, T2> : Iterator<(T1, T2)>
{
    private readonly Iterator<T1> _iterator1;
    private readonly Iterator<T2> _iterator2;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipIterator{T1, T2}"/> class.
    /// </summary>
    /// <param name="iterable1">The first iterable</param>
    /// <param name="iterable2">The second iterable</param>
    public ZipIterator(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2)
    {
        if (iterable1 is null)
        {
            throw TypeError.ArgNone("zip", "iterable1");
        }

        if (iterable2 is null)
        {
            throw TypeError.ArgNone("zip", "iterable2");
        }

        _iterator1 = Exports.Iter(iterable1);
        _iterator2 = Exports.Iter(iterable2);
    }

    /// <inheritdoc/>
    public override (T1, T2) __Next__()
    {
        var value1 = _iterator1.__Next__();
        var value2 = _iterator2.__Next__();
        return (value1, value2);
    }
}

/// <summary>
/// Iterator that yields tuples containing elements from three iterables.
/// </summary>
/// <typeparam name="T1">The type of elements in the first iterable</typeparam>
/// <typeparam name="T2">The type of elements in the second iterable</typeparam>
/// <typeparam name="T3">The type of elements in the third iterable</typeparam>
public class ZipIterator<T1, T2, T3> : Iterator<(T1, T2, T3)>
{
    private readonly Iterator<T1> _iterator1;
    private readonly Iterator<T2> _iterator2;
    private readonly Iterator<T3> _iterator3;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipIterator{T1, T2, T3}"/> class.
    /// </summary>
    /// <param name="iterable1">The first iterable</param>
    /// <param name="iterable2">The second iterable</param>
    /// <param name="iterable3">The third iterable</param>
    public ZipIterator(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, IEnumerable<T3> iterable3)
    {
        if (iterable1 is null)
        {
            throw TypeError.ArgNone("zip", "iterable1");
        }

        if (iterable2 is null)
        {
            throw TypeError.ArgNone("zip", "iterable2");
        }

        if (iterable3 is null)
        {
            throw TypeError.ArgNone("zip", "iterable3");
        }

        _iterator1 = Exports.Iter(iterable1);
        _iterator2 = Exports.Iter(iterable2);
        _iterator3 = Exports.Iter(iterable3);
    }

    /// <inheritdoc/>
    public override (T1, T2, T3) __Next__()
    {
        var value1 = _iterator1.__Next__();
        var value2 = _iterator2.__Next__();
        var value3 = _iterator3.__Next__();
        return (value1, value2, value3);
    }
}

public static partial class Exports
{
    /// <summary>
    /// Make an iterator that aggregates elements from two iterables.
    /// Returns an iterator of tuples, where the i-th tuple contains the i-th element
    /// from each of the argument sequences. The iterator stops when the shortest
    /// input iterable is exhausted.
    /// </summary>
    /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
    /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
    /// <param name="iterable1">The first iterable</param>
    /// <param name="iterable2">The second iterable</param>
    /// <returns>A zip iterator</returns>
    public static ZipIterator<T1, T2> Zip<T1, T2>(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2)
    {
        return new ZipIterator<T1, T2>(iterable1, iterable2);
    }

    /// <summary>
    /// Make an iterator that aggregates elements from three iterables.
    /// Returns an iterator of tuples, where the i-th tuple contains the i-th element
    /// from each of the argument sequences. The iterator stops when the shortest
    /// input iterable is exhausted.
    /// </summary>
    /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
    /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
    /// <typeparam name="T3">The type of elements in the third iterable</typeparam>
    /// <param name="iterable1">The first iterable</param>
    /// <param name="iterable2">The second iterable</param>
    /// <param name="iterable3">The third iterable</param>
    /// <returns>A zip iterator</returns>
    public static ZipIterator<T1, T2, T3> Zip<T1, T2, T3>(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, IEnumerable<T3> iterable3)
    {
        return new ZipIterator<T1, T2, T3>(iterable1, iterable2, iterable3);
    }
}
