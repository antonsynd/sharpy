namespace Sharpy.Core;

using static Sharpy.Sys.Exports;

/// <summary>
/// Global builtin functions available in all Sharpy programs
/// </summary>
public static partial class Exports
{
    /// <summary>
    /// Print values to standard output, matching Python's print() behavior.
    /// Values are converted to strings using ToString() and separated by the separator.
    /// </summary>
    /// <param name="values">Values to print</param>
    public static void Print(params object?[] values)
    {
        PrintWithOptions(values, sep: " ", end: "\n", file: Stdout, flush: false);
    }

    /// <summary>
    /// Print values to standard output with custom options.
    /// This is the full Python-compatible print function.
    /// </summary>
    /// <param name="values">Values to print</param>
    /// <param name="sep">Separator between values (default: space)</param>
    /// <param name="end">String appended after the last value (default: newline)</param>
    /// <param name="file">Output stream (default: stdout)</param>
    /// <param name="flush">Whether to flush the stream (default: false)</param>
    public static void PrintWithOptions(object?[] values, string sep = " ", string end = "\n", uint file = Stdout, bool flush = false)
    {
        if (file == Stddev)
        {
            return;
        }

        var textWriter = file == Stdout ? Console.Out : Console.Error;
        var output = string.Join(sep, values.Select(v => v?.ToString() ?? "None"));

        if (end == "\n")
        {
            textWriter.WriteLine(output);
        }
        else
        {
            textWriter.Write(output);
            if (!string.IsNullOrEmpty(end))
            {
                textWriter.Write(end);
            }
        }

        if (flush)
        {
            textWriter.Flush();
        }
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
