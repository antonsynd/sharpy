using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Itertools
    {
        /// <summary>Make an iterator that drops elements from the iterable as long as the predicate is true; afterwards, returns every element.</summary>
        public static DropwhileIterator<T> Dropwhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return new DropwhileIterator<T>(predicate, iterable);
        }
    }

    /// <summary>Iterator that drops elements while a predicate is true, then yields all remaining elements.</summary>
    public class DropwhileIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, bool> _predicate;
        private bool _dropping;
        private bool _exhausted;

        internal DropwhileIterator(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            _enumerator = iterable.GetEnumerator();
            _predicate = predicate;
            _dropping = true;
            _exhausted = false;
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (_exhausted)
            {
                _current = default;
                return false;
            }

            while (_enumerator.MoveNext())
            {
                if (_dropping)
                {
                    if (_predicate(_enumerator.Current))
                    {
                        continue;
                    }

                    _dropping = false;
                }

                _current = _enumerator.Current;
                return true;
            }

            _exhausted = true;
            _current = default;
            return false;
        }
    }
}
