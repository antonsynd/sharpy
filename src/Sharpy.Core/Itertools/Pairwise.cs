using System.Collections.Generic;

namespace Sharpy
{
    internal static partial class Itertools
    {
        public static PairwiseIterator<T> Pairwise<T>(IEnumerable<T> iterable)
        {
            return new PairwiseIterator<T>(iterable);
        }
    }

    public class PairwiseIterator<T> : Iterator<(T, T)>
    {
        private readonly IEnumerator<T> _enumerator;
        private T? _previous;
        private bool _started;
        private bool _exhausted;

        internal PairwiseIterator(IEnumerable<T> iterable)
        {
            _enumerator = iterable.GetEnumerator();
            _started = false;
            _exhausted = false;
        }

        public override bool MoveNext()
        {
            if (_exhausted)
            {
                _current = default;
                return false;
            }

            if (!_started)
            {
                _started = true;
                if (!_enumerator.MoveNext())
                {
                    _exhausted = true;
                    _current = default;
                    return false;
                }

                _previous = _enumerator.Current;
            }

            if (!_enumerator.MoveNext())
            {
                _exhausted = true;
                _current = default;
                return false;
            }

            _current = (_previous!, _enumerator.Current);
            _previous = _enumerator.Current;
            return true;
        }
    }
}
