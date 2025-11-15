namespace Sharpy.Core;

using Collections.Interfaces;

public static partial class Exports
{
    /// <summary>
    /// Return a new sorted list from the items in iterable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable</typeparam>
    /// <param name="iterable">The iterable to sort</param>
    /// <returns>A new sorted list</returns>
    public static List<T> Sorted<T>(IIterable<T> iterable)
    {
        return SortedImpl(iterable, ComparerAdapter<T>.Instance, reverse: false);
    }

    public static List<T> Sorted<T, TKey>(IIterable<T> iterable, Func<T, TKey> key)
    {
        if (key is null)
        {
            throw TypeError.ArgNone("sorted", "key");
        }
        return SortedImpl(iterable, KeyComparerFactory<T, TKey>.Create(key), reverse: false);
    }

    public static List<T> Sorted<T>(IIterable<T> iterable, bool reverse)
    {
        return SortedImpl(iterable, ComparerAdapter<T>.Instance, reverse);
    }

    public static List<T> Sorted<T, TKey>(IIterable<T> iterable, Func<T, TKey> key, bool reverse)
    {
        if (key is null)
        {
            throw TypeError.ArgNone("sorted", "key");
        }
        return SortedImpl(iterable, KeyComparerFactory<T, TKey>.Create(key), reverse);
    }

    private static List<T> SortedImpl<T>(IIterable<T> iterable, IComparer<T> comparer, bool reverse)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("sorted", "iterable");
        }

        var systemList = new System.Collections.Generic.List<T>();
        foreach (var item in iterable)
        {
            systemList.Add(item);
        }

        systemList.Sort(comparer);
        if (reverse)
        {
            systemList.Reverse();
        }
        return new List<T>(systemList);
    }
}
