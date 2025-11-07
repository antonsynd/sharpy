namespace Sharpy;

using Collections.Interfaces;

public readonly partial struct Slice(int start, int end, int step = 1) : ISized
{
    public readonly int start = start;
    public readonly int end = end;
    public readonly int step = step;

    public static uint Len(int start, int end, int step)
    {
        // Efficient ceil division (from ChatGPT)
        var length = end - start;
        return (uint)((length + step - 1) / step);
    }

    public static Slice FromRange(System.Range range)
    {
        return new Slice(range.Start.Value, range.End.Value);
    }

    internal static (uint, uint) Normalize(int start, int end, uint max)
    {
        return (Index.Normalize(start, max, true, false), Index.Normalize(end, max, true, false));
    }
}
