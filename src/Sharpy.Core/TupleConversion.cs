using System.Collections.Generic;
using System.Linq;

namespace Sharpy.Core;

/// <summary>
/// Type conversion functions for tuple
/// </summary>
public static partial class Exports
{
    /// <summary>
    /// Helper method to convert iterable to list
    /// </summary>
    private static System.Collections.Generic.List<object> IterableToList(Collections.Interfaces.IIterable<object> iterable)
    {
        var items = new System.Collections.Generic.List<object>();
        var iterator = iterable.__Iter__();

        while (true)
        {
            try
            {
                items.Add(iterator.__Next__());
            }
            catch (StopIteration)
            {
                break;
            }
        }
        return items;
    }

    /// <summary>
    /// Convert iterable to tuple (ValueTuple)
    /// </summary>
    public static (T1, T2) Tuple<T1, T2>(Collections.Interfaces.IIterable<object> iterable)
    {
        var items = IterableToList(iterable);

        if (items.Count != 2)
        {
            throw new ValueError($"Expected 2 items for tuple, got {items.Count}");
        }

        return ((T1)items[0], (T2)items[1]);
    }

    /// <summary>
    /// Convert iterable to tuple (ValueTuple with 3 items)
    /// </summary>
    public static (T1, T2, T3) Tuple<T1, T2, T3>(Collections.Interfaces.IIterable<object> iterable)
    {
        var items = IterableToList(iterable);

        if (items.Count != 3)
        {
            throw new ValueError($"Expected 3 items for tuple, got {items.Count}");
        }

        return ((T1)items[0], (T2)items[1], (T3)items[2]);
    }

    /// <summary>
    /// Convert IEnumerable to tuple (ValueTuple)
    /// </summary>
    public static (T1, T2) Tuple<T1, T2>(IEnumerable<object> enumerable)
    {
        var items = enumerable.ToList();

        if (items.Count != 2)
        {
            throw new ValueError($"Expected 2 items for tuple, got {items.Count}");
        }

        return ((T1)items[0], (T2)items[1]);
    }

    /// <summary>
    /// Convert IEnumerable to tuple (ValueTuple with 3 items)
    /// </summary>
    public static (T1, T2, T3) Tuple<T1, T2, T3>(IEnumerable<object> enumerable)
    {
        var items = enumerable.ToList();

        if (items.Count != 3)
        {
            throw new ValueError($"Expected 3 items for tuple, got {items.Count}");
        }

        return ((T1)items[0], (T2)items[1], (T3)items[2]);
    }
}
