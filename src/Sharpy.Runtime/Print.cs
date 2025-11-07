namespace Sharpy;

using static Sharpy.Sys.Exports;

public static partial class Exports
{
    public static void Print(Object? obj, uint file = Stdout, bool flush = false)
    {
        var result = obj?.__Str__() ?? "None";

        _Print(result, file, flush);
    }

    public static void Print(PrintArguments<Object?> args, uint file = Stdout, bool flush = false)
    {
        var lastIndex = (uint)args.args.Length - 1;
        uint i = 0;

        foreach (var obj in args.args)
        {
            var result = obj?.__Str__() ?? "None";

            _Print(result, file, flush && i == lastIndex, i == lastIndex);

            ++i;
        }
    }

    public static void Print(object? obj, uint file = Stdout, bool flush = false)
    {
        var result = obj?.ToString() ?? "None";

        _Print(result, file, flush);
    }

    public static void Print(PrintArguments<object?> args, uint file = Stdout, bool flush = false)
    {
        var lastIndex = (uint)args.args.Length - 1;
        uint i = 0;

        foreach (var obj in args.args)
        {
            var result = obj?.ToString() ?? "None";

            _Print(result, file, flush && i == lastIndex, i == lastIndex);

            ++i;
        }
    }

    private static void _Print(string s, uint file = Stdout, bool flush = false, bool newline = true)
    {
        if (file == Stddev)
        {
            return;
        }

        var textWriter = file == Stdout ? Console.Out : Console.Error;

        if (newline)
        {
            textWriter.WriteLine(s);
        }
        else
        {
            textWriter.Write(s);
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
