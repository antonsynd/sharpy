using Sharpy.Core;

namespace Sharpy.Itertools;

internal static partial class Exports
{
    public static Iterator<T> Cycle<T>(IEnumerable<T> iterable)
    {
        return new CycleIterator<T>(iterable);
    }
}

file class CycleIterator<T> : Iterator<T>
{
    private Sharpy.Core.List<T> _saved;
    private uint _currentIndex;

    private readonly IEnumerator<T> _enumerator;
    private bool _iteratorEmpty;

    internal CycleIterator(IEnumerable<T> iterable)
    {
        _saved = [];
        _currentIndex = 0;

        _enumerator = iterable.GetEnumerator();
        _iteratorEmpty = false;
    }

    public override T __Next__()
    {
        // Iterate through the iterator first, saving each item as we go along
        if (!_iteratorEmpty)
        {
            if (_enumerator.MoveNext())
            {
                var res = _enumerator.Current;

                _saved.Append(res);

                return res;
            }
            else
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
