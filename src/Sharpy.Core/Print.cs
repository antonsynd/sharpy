namespace Sharpy.Core;

using static Sharpy.Sys.Exports;

public static partial class Exports
{
    public static void Print(Object? obj, string sep = " ", string end = "\n", uint file = Stdout, bool flush = false)
    {
        var result = obj?.__Str__() ?? "None";

        _Print(result, end, file, flush);
    }

    public static void Print(PrintArguments<Object?> args, string sep = " ", string end = "\n", uint file = Stdout, bool flush = false)
    {
        var lastIndex = (uint)args.args.Length - 1;
        uint i = 0;

        foreach (var obj in args.args)
        {
            var result = obj?.__Str__() ?? "None";

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

    public static void Print(object? obj, string sep = " ", string end = "\n", uint file = Stdout, bool flush = false)
    {
        var result = obj?.ToString() ?? "None";

        _Print(result, end, file, flush);
    }

    public static void Print(PrintArguments<object?> args, string sep = " ", string end = "\n", uint file = Stdout, bool flush = false)
    {
        var lastIndex = (uint)args.args.Length - 1;
        uint i = 0;

        foreach (var obj in args.args)
        {
            var result = obj?.ToString() ?? "None";

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
