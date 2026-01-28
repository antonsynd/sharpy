namespace Sharpy.Core;

public readonly partial struct Slice(int start, int end, int step = 1)
{
    public readonly int start = start;
    public readonly int end = end;
    public readonly int step = step;

    public static int Len(int start, int end, int step)
    {
        // Efficient ceil division (from ChatGPT)
        var length = end - start;
        return (length + step - 1) / step;
    }

    public static Slice FromRange(System.Range range)
    {
        return new Slice(range.Start.Value, range.End.Value);
    }

    internal static (int, int) Normalize(int start, int end, int max)
    {
        return (Index.Normalize(start, max, true, false), Index.Normalize(end, max, true, false));
    }
}
