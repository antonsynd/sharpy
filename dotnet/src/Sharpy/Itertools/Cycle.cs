namespace Sharpy.Itertools;

using Collections.Interfaces;

public static partial class Exports
{
    public static Iterator<T> Cycle<T>(IIterable<T> iterable)
    {
        return new CycleIterator<T>(iterable);
    }
}

file class CycleIterator<T> : Iterator<T>
{
    private Sharpy.List<T> _saved;
    private uint _currentIndex;

    private readonly Iterator<T> _iterator;
    private bool _iteratorEmpty;

    internal CycleIterator(IIterable<T> iterable)
    {
        _saved = [];
        _currentIndex = 0;

        _iterator = iterable.__Iter__();
        _iteratorEmpty = false;
    }

    public override T __Next__()
    {
        // Iterate through the iterator first, saving each item as we go along
        if (!_iteratorEmpty)
        {
            try
            {
                var res = _iterator.__Next__();

                _saved.Append(res);

                return res;
            }
            catch (StopIteration e)
            {
                _iteratorEmpty = true;
            }
        }

        var numSaved = _saved.__Len__();

        // Nothing saved means nothing to iterate through
        if (numSaved == 0)
        {
            throw new StopIteration();
        }

        // Cycle back to the front
        if (_currentIndex >= numSaved)
        {
            _currentIndex = 0;
        }

        var savedRes = _saved[(int)_currentIndex];

        ++_currentIndex;

        return savedRes;
    }
}
