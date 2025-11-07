namespace Sharpy.Itertools;

using System.Numerics;

internal static partial class Exports
{
    public static Iterator<T> Count<T>(T start, T step) where T : INumber<T>
    {
        return new CountIterator<T>(start, step);
    }
}

file class CountIterator<T> : Iterator<T> where T : INumber<T>
{
    private T _current;
    private readonly T _step;

    internal CountIterator(T start, T step)
    {
        _current = start;
        _step = step;
    }

    public override T __Next__()
    {
        var res = _current;

        _current += _step;

        return res;
    }
}
