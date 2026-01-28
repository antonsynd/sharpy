using System.Collections.Generic;
using System.Linq;

namespace Sharpy.Core;

/// <summary>
/// Type conversion functions for tuple
/// </summary>
public static partial class Exports
{
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
