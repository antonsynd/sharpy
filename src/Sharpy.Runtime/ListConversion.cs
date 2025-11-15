using System.Collections.Generic;

namespace Sharpy;

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
        var list = new List<T>();
        foreach (var item in enumerable)
        {
            list.Add(item);
        }
        return list;
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
        var list = new List<T>();
        foreach (var item in other)
        {
            list.Add(item);
        }
        return list;
    }
}
