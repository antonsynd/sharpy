using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Itertools
    {
        public static FilterfalseIterator<T> Filterfalse<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return new FilterfalseIterator<T>(predicate, iterable);
        }
    }

    public class FilterfalseIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, bool> _predicate;
        private bool _exhausted;

        internal FilterfalseIterator(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            _enumerator = iterable.GetEnumerator();
            _predicate = predicate;
            _exhausted = false;
        }

        public override bool MoveNext()
        {
            if (_exhausted)
            {
                _current = default;
                return false;
            }

            while (_enumerator.MoveNext())
            {
                if (!_predicate(_enumerator.Current))
                {
                    _current = _enumerator.Current;
                    return true;
                }
            }

            _exhausted = true;
            _current = default;
            return false;
        }
    }
}
