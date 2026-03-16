using System;
using System.Linq;

namespace Sharpy
{
    using static Sharpy.Sys;

    public static partial class Builtins
    {
        /// <summary>
        /// Print values to standard output, matching Python's print() behavior.
        /// Values are converted to strings using ToString() and separated by the separator.
        /// </summary>
        /// <param name="values">Values to print</param>
        /// <example>
        /// <code>
        /// print("hello")           # hello
        /// print(1, 2, 3)           # 1 2 3
        /// print("a", "b", sep=",") # a,b
        /// </code>
        /// </example>
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
        internal static void PrintWithOptions(object?[] values, string sep = " ", string end = "\n", uint file = Stdout, bool flush = false)
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
        /// Format a value for print output with Python-compatible representation.
        /// Delegates to <see cref="Str(object)"/> for consistent formatting.
        /// </summary>
        private static string FormatValue(object? v)
        {
            return v is null ? "None" : Str(v);
        }
    }
}
