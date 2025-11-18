namespace Sharpy.Core;

using Collections.Interfaces;
using System.Linq;

/// <summary>
/// LINQ-style extension methods for Sharpy iterables to enable natural C# integration.
/// </summary>
/// <remarks>
/// These extension methods allow Sharpy IIterable types to work seamlessly with C# LINQ
/// query syntax and standard LINQ operators, bridging the gap between Sharpy's iterator
/// protocol and C#'s enumerable patterns.
/// </remarks>
public static class IterableLinqExtensions
{
    /// <summary>
    /// Projects each element of a sequence into a new form.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by selector.</typeparam>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>An IEnumerable whose elements are the result of invoking the transform function on each element of source.</returns>
    /// <exception cref="ArgumentNullException">source or selector is null.</exception>
    public static IEnumerable<TResult> Select<TSource, TResult>(
        this IIterable<TSource> source,
        Func<TSource, TResult> selector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return source.AsEnumerable().Select(selector);
    }

    /// <summary>
    /// Filters a sequence of values based on a predicate.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An IIterable to filter.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>An IEnumerable that contains elements from the input sequence that satisfy the condition.</returns>
    /// <exception cref="ArgumentNullException">source or predicate is null.</exception>
    public static IEnumerable<TSource> Where<TSource>(
        this IIterable<TSource> source,
        Func<TSource, bool> predicate)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return source.AsEnumerable().Where(predicate);
    }

    /// <summary>
    /// Returns the first element of a sequence.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">The IIterable to return the first element of.</param>
    /// <returns>The first element in the specified sequence.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    /// <exception cref="InvalidOperationException">The source sequence is empty.</exception>
    public static TSource First<TSource>(this IIterable<TSource> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().First();
    }

    /// <summary>
    /// Returns the first element of a sequence, or a default value if the sequence contains no elements.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">The IIterable to return the first element of.</param>
    /// <returns>default(TSource) if source is empty; otherwise, the first element in source.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static TSource? FirstOrDefault<TSource>(this IIterable<TSource> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().FirstOrDefault();
    }

    /// <summary>
    /// Returns the last element of a sequence.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An IIterable to return the last element of.</param>
    /// <returns>The value at the last position in the source sequence.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    /// <exception cref="InvalidOperationException">The source sequence is empty.</exception>
    public static TSource Last<TSource>(this IIterable<TSource> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().Last();
    }

    /// <summary>
    /// Returns the last element of a sequence, or a default value if the sequence contains no elements.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An IIterable to return the last element of.</param>
    /// <returns>default(TSource) if the source sequence is empty; otherwise, the last element in the IIterable.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static TSource? LastOrDefault<TSource>(this IIterable<TSource> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().LastOrDefault();
    }

    /// <summary>
    /// Determines whether any element of a sequence satisfies a condition.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An IIterable whose elements to apply the predicate to.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>true if any elements in the source sequence pass the test in the specified predicate; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">source or predicate is null.</exception>
    public static bool Any<TSource>(
        this IIterable<TSource> source,
        Func<TSource, bool> predicate)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return source.AsEnumerable().Any(predicate);
    }

    /// <summary>
    /// Determines whether a sequence contains any elements.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">The IIterable to check for emptiness.</param>
    /// <returns>true if the source sequence contains any elements; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static bool Any<TSource>(this IIterable<TSource> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().Any();
    }

    /// <summary>
    /// Determines whether all elements of a sequence satisfy a condition.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An IIterable that contains the elements to apply the predicate to.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>true if every element of the source sequence passes the test in the specified predicate, or if the sequence is empty; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">source or predicate is null.</exception>
    public static bool All<TSource>(
        this IIterable<TSource> source,
        Func<TSource, bool> predicate)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return source.AsEnumerable().All(predicate);
    }

    /// <summary>
    /// Returns the number of elements in a sequence.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <returns>The number of elements in the input sequence.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static int Count<TSource>(this IIterable<TSource> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().Count();
    }

    /// <summary>
    /// Converts a Sharpy iterable to a standard C# enumerable view.
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable.</typeparam>
    /// <param name="iterable">The Sharpy iterable to convert.</param>
    /// <returns>The same sequence as an IEnumerable.</returns>
    /// <remarks>
    /// Since IIterable already extends IEnumerable, this is effectively a cast operation
    /// that makes the enumerable nature explicit for use with LINQ and other C# APIs.
    /// </remarks>
    private static IEnumerable<T> AsEnumerable<T>(this IIterable<T> iterable)
    {
        // IIterable already extends IEnumerable, so this is just a cast
        return iterable;
    }

    /// <summary>
    /// Converts the sequence to a List&lt;T&gt;.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">The IIterable to create a List from.</param>
    /// <returns>A List that contains elements from the input sequence.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static System.Collections.Generic.List<TSource> ToList<TSource>(this IIterable<TSource> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().ToList();
    }

    /// <summary>
    /// Converts the sequence to an array.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An IIterable to create an array from.</param>
    /// <returns>An array that contains the elements from the input sequence.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static TSource[] ToArray<TSource>(this IIterable<TSource> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().ToArray();
    }

    /// <summary>
    /// Bypasses a specified number of elements in a sequence and then returns the remaining elements.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An IIterable to return elements from.</param>
    /// <param name="count">The number of elements to skip before returning the remaining elements.</param>
    /// <returns>An IEnumerable that contains the elements that occur after the specified index in the input sequence.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static IEnumerable<TSource> Skip<TSource>(this IIterable<TSource> source, int count)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().Skip(count);
    }

    /// <summary>
    /// Returns a specified number of contiguous elements from the start of a sequence.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="count">The number of elements to return.</param>
    /// <returns>An IEnumerable that contains the specified number of elements from the start of the input sequence.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static IEnumerable<TSource> Take<TSource>(this IIterable<TSource> source, int count)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().Take(count);
    }

    /// <summary>
    /// Sorts the elements of a sequence in ascending order according to a key.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by keySelector.</typeparam>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <returns>An IOrderedEnumerable whose elements are sorted according to a key.</returns>
    /// <exception cref="ArgumentNullException">source or keySelector is null.</exception>
    public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(
        this IIterable<TSource> source,
        Func<TSource, TKey> keySelector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (keySelector is null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        return source.AsEnumerable().OrderBy(keySelector);
    }

    /// <summary>
    /// Sorts the elements of a sequence in descending order according to a key.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by keySelector.</typeparam>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <returns>An IOrderedEnumerable whose elements are sorted in descending order according to a key.</returns>
    /// <exception cref="ArgumentNullException">source or keySelector is null.</exception>
    public static IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(
        this IIterable<TSource> source,
        Func<TSource, TKey> keySelector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (keySelector is null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        return source.AsEnumerable().OrderByDescending(keySelector);
    }
}
