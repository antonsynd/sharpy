using System;
namespace Sharpy
{
    using static Sharpy.Sys;

    public static partial class Builtins
    {
        // Note: The primary Print(params object?[] values) is defined in Builtins/Builtins.cs
        // PrintWithOptions provides full control over sep, end, file, and flush parameters.
        // The PrintArguments<T> overloads are kept for backwards compatibility with code that
        // explicitly uses PrintArguments.

        // Note: Removed Print(Object? obj, ...) and Print(object? obj, ...) overloads as they
        // conflict with Print(params object?[] values). Use PrintWithOptions for custom options.

        public static void Print(PrintArguments<object?> args, string sep = " ", string end = "\n", uint file = Stdout, bool flush = false)
        {
            var lastIndex = (uint)args.args.Length - 1;
            uint i = 0;

            foreach (var obj in args.args)
            {
                var result = FormatValue(obj);

                if (i < lastIndex)
                {
                    _Print(result, sep, file, false, true);  // Print separator after non-last items
                }
                else
                {
                    _Print(result, end, file, flush, true);  // Print end terminator after last item
                }

                ++i;
            }
        }

        private static void _Print(string s, string terminator, uint file = Stdout, bool flush = false, bool useTerminator = true)
        {
            if (file == Stddev)
            {
                return;
            }

            var textWriter = file == Stdout ? Console.Out : Console.Error;

            if (useTerminator && terminator == "\n")
            {
                textWriter.WriteLine(s);
            }
            else
            {
                textWriter.Write(s);
                if (useTerminator && !string.IsNullOrEmpty(terminator))
                {
                    textWriter.Write(terminator);
                }
            }

            if (flush)
            {
                textWriter.Flush();
            }
        }
    }

    public class PrintArguments<T>
    {
        public readonly T[] args;

        public PrintArguments(params T[] args)
        {
            this.args = args;
        }
    }
}
