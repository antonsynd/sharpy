namespace Sharpy.Itertools;

public static partial class Exports
{
    public static Iterator<T> Repeat<T>(T elem)
    {
        return new RepeatIterator<T>(elem);
    }

    public static Iterator<T> Repeat<T>(T elem, uint n)
    {
        return new RepeatIterator<T>(elem, n);
    }
}

file class RepeatIterator<T> : Iterator<T>
{
    private readonly T _elem;
    private readonly bool _infinite;
    private bool _active;
    private uint _n;

    internal RepeatIterator(T elem)
    {
        _elem = elem;
        _infinite = true;
        _active = true;
        _n = 0;
    }

    internal RepeatIterator(T elem, uint n)
    {
        _elem = elem;
        _infinite = false;
        _active = true;
        _n = n;
    }

    public override T __Next__()
    {
        if (_infinite)
        {
            return _elem;
        }

        if (_active)
        {
            if (_n > 0)
            {
                --_n;
                return _elem;
            }

            _active = false;
            return _elem;
        }

        throw new StopIteration();
    }
}
