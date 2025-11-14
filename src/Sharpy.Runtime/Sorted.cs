namespace Sharpy;

using Collections.Interfaces;
using System.Linq;

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
        if (iterable is null)
        {
            throw TypeError.ArgNone("sorted", "iterable");
        }

        var systemList = new System.Collections.Generic.List<T>();
        foreach (var item in iterable)
        {
            systemList.Add(item);
        }

        systemList.Sort();
        
        var result = new List<T>();
        foreach (var item in systemList)
        {
            result.Append(item);
        }
        return result;
    }

    /// <summary>
    /// Return a new sorted list from the items in iterable using a key function.
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable</typeparam>
    /// <typeparam name="TKey">The type of the key to sort by</typeparam>
    /// <param name="iterable">The iterable to sort</param>
    /// <param name="key">A function to extract a comparison key from each element</param>
    /// <returns>A new sorted list</returns>
    public static List<T> Sorted<T, TKey>(IIterable<T> iterable, Func<T, TKey> key)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("sorted", "iterable");
        }

        if (key is null)
        {
            throw TypeError.ArgNone("sorted", "key");
        }

        var systemList = new System.Collections.Generic.List<T>();
        foreach (var item in iterable)
        {
            systemList.Add(item);
        }

        systemList.Sort((a, b) => Comparer<TKey>.Default.Compare(key(a), key(b)));
        
        var result = new List<T>();
        foreach (var item in systemList)
        {
            result.Append(item);
        }
        return result;
    }

    /// <summary>
    /// Return a new sorted list from the items in iterable in reverse order.
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable</typeparam>
    /// <param name="iterable">The iterable to sort</param>
    /// <param name="reverse">If true, sort in descending order</param>
    /// <returns>A new sorted list</returns>
    public static List<T> Sorted<T>(IIterable<T> iterable, bool reverse)
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

        systemList.Sort();
        if (reverse)
        {
            systemList.Reverse();
        }
        
        var result = new List<T>();
        foreach (var item in systemList)
        {
            result.Append(item);
        }
        return result;
    }

    /// <summary>
    /// Return a new sorted list from the items in iterable using a key function in reverse order.
    /// </summary>
    /// <typeparam name="T">The type of elements in the iterable</typeparam>
    /// <typeparam name="TKey">The type of the key to sort by</typeparam>
    /// <param name="iterable">The iterable to sort</param>
    /// <param name="key">A function to extract a comparison key from each element</param>
    /// <param name="reverse">If true, sort in descending order</param>
    /// <returns>A new sorted list</returns>
    public static List<T> Sorted<T, TKey>(IIterable<T> iterable, Func<T, TKey> key, bool reverse)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("sorted", "iterable");
        }

        if (key is null)
        {
            throw TypeError.ArgNone("sorted", "key");
        }

        var systemList = new System.Collections.Generic.List<T>();
        foreach (var item in iterable)
        {
            systemList.Add(item);
        }

        systemList.Sort((a, b) =>
        {
            var comparison = Comparer<TKey>.Default.Compare(key(a), key(b));
            return reverse ? -comparison : comparison;
        });
        
        var result = new List<T>();
        foreach (var item in systemList)
        {
            result.Append(item);
        }
        return result;
    }
}
