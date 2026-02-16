using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
namespace Sharpy
{
    using static Sharpy.Sys;

    /// <summary>
    /// Global builtin functions available in all Sharpy programs
    /// </summary>
    [SharpyModule("builtins")]
    public static partial class Builtins
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
            var output = string.Join(sep, values.Select(FormatValue));

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
        /// Get the length of a collection or string.
        /// This is the fallback overload for dynamically-typed scenarios.
        /// </summary>
        public static int Len(object obj)
        {
            if (obj is null)
            {
                throw TypeError.ArgNone("len", "sized");
            }

            // Fast path for common types
            if (obj is string s)
                return s.Length;
            if (obj is Array arr)
                return arr.Length;
            if (obj is System.Collections.ICollection collection)
                return collection.Count;

            // Check for generic ICollection<T> or IReadOnlyCollection<T> via reflection
            // This handles types like Set<T> that implement ICollection<T> but not non-generic ICollection
            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType)
                {
                    var genericDef = iface.GetGenericTypeDefinition();
                    if (genericDef == typeof(ICollection<>) || genericDef == typeof(IReadOnlyCollection<>))
                    {
                        var countProp = iface.GetProperty("Count");
                        if (countProp is not null)
                        {
                            return (int)countProp.GetValue(obj)!;
                        }
                    }
                }
            }

            throw new TypeError($"object of type '{obj.GetType().Name}' has no len()");
        }

        /// <summary>
        /// Format a value for print output with Python-compatible representation.
        /// Handles null, booleans, and floating-point types specially.
        /// </summary>
        private static string FormatValue(object? v)
        {
            if (v is null)
            {
                return "None";
            }

            if (v is bool b)
            {
                return b ? "True" : "False";
            }

            if (v is double d)
            {
                return FormatFloat(d);
            }

            if (v is float f)
            {
                return FormatFloat(f);
            }

            return v.ToString() ?? "";
        }
    }
}
