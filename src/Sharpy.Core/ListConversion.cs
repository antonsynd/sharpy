using System.Collections.Generic;

namespace Sharpy.Core;

/// <summary>
/// Type conversion functions for list
/// </summary>
public static partial class Exports
{
    /// <summary>
    /// Convert iterable to list
    /// </summary>
    public static List<T> List<T>(Collections.Interfaces.IIterable<T> iterable)
    {
        var list = new List<T>();
        var iterator = iterable.__Iter__();

        while (true)
        {
            try
            {
                var item = iterator.__Next__();
                list.Add(item);
            }
            catch (StopIteration)
            {
                break;
            }
        }

        return list;
    }

    /// <summary>
    /// Convert IEnumerable to list
    /// </summary>
    public static List<T> List<T>(IEnumerable<T> enumerable)
    {
        return new List<T>(enumerable);
    }

    /// <summary>
    /// Create empty list
    /// </summary>
    public static List<T> List<T>()
    {
        return new List<T>();
    }

    /// <summary>
    /// Convert list to list (copy)
    /// </summary>
    public static List<T> List<T>(List<T> other)
    {
        return other.Copy();
    }
}
