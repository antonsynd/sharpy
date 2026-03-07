using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Itertools
    {
        public static Iterator<T> Cycle<T>(IEnumerable<T> iterable)
        {
            return new CycleIterator<T>(iterable);
        }
    }

    internal class CycleIterator<T> : Iterator<T>
    {
        private Sharpy.List<T> _saved;
        private uint _currentIndex;

        private readonly IEnumerator<T> _enumerator;
        private bool _iteratorEmpty;

        internal CycleIterator(IEnumerable<T> iterable)
        {
            _saved = new Sharpy.List<T>();
            _currentIndex = 0;

            _enumerator = iterable.GetEnumerator();
            _iteratorEmpty = false;
        }

        public override bool MoveNext()
        {
            // Iterate through the iterator first, saving each item as we go along
            if (!_iteratorEmpty)
            {
                if (_enumerator.MoveNext())
                {
                    _current = _enumerator.Current;
                    _saved.Append(_current);
                    return true;
                }
                else
                {
                    _iteratorEmpty = true;
                }
            }

            var numSaved = ((System.Collections.Generic.ICollection<T>)_saved).Count;

            // Nothing saved means nothing to iterate through
            if (numSaved == 0)
            {
                _current = default;
                return false;
            }

            // Cycle back to the front
            if (_currentIndex >= numSaved)
            {
                _currentIndex = 0;
            }

            _current = _saved[(int)_currentIndex];
            ++_currentIndex;
            return true;
        }
    }
}
