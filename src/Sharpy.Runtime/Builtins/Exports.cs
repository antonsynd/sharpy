namespace Sharpy;

/// <summary>
/// Global builtin functions available in all Sharpy programs
/// </summary>
public static partial class Exports
{
    /// <summary>
    /// Print values to standard output
    /// </summary>
    public static void Print(params object?[] values)
    {
        var separator = " ";
        var output = string.Join(separator, values.Select(v => v?.ToString() ?? "None"));
        Console.WriteLine(output);
    }

    /// <summary>
    /// Get the length of a collection or string
    /// </summary>
    public static int Len(object obj)
    {
        return obj switch
        {
            string s => s.Length,
            Array arr => arr.Length,
            System.Collections.ICollection collection => collection.Count,
            _ => throw new TypeError($"object of type '{obj.GetType().Name}' has no len()")
        };
    }
}
