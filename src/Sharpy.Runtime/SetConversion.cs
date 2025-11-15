using System.Collections.Generic;

namespace Sharpy;

/// <summary>
/// Type conversion functions for set
/// </summary>
public static partial class Exports
{
    /// <summary>
    /// Convert iterable to set
    /// </summary>
    public static Set<T> Set<T>(Collections.Interfaces.IIterable<T> iterable)
    {
        var set = new Set<T>();
        var iterator = iterable.__Iter__();
        
        while (true)
        {
            try
            {
                var item = iterator.__Next__();
                set.Add(item);
            }
            catch (StopIteration)
            {
                break;
            }
        }
        
        return set;
    }

    /// <summary>
    /// Convert IEnumerable to set
    /// </summary>
    public static Set<T> Set<T>(IEnumerable<T> enumerable)
    {
        var set = new Set<T>();
        foreach (var item in enumerable)
        {
            set.Add(item);
        }
        return set;
    }

    /// <summary>
    /// Create empty set
    /// </summary>
    public static Set<T> Set<T>()
    {
        return new Set<T>();
    }

    /// <summary>
    /// Convert set to set (copy)
    /// </summary>
    public static Set<T> Set<T>(Set<T> other)
    {
        var set = new Set<T>();
        foreach (var item in other)
        {
            set.Add(item);
        }
        return set;
    }
}
