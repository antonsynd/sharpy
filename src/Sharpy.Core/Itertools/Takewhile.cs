using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Itertools
    {
        /// <summary>Make an iterator that returns elements from the iterable as long as the predicate is true.</summary>
        public static TakewhileIterator<T> Takewhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return new TakewhileIterator<T>(predicate, iterable);
        }
    }

    /// <summary>Iterator that yields elements while a predicate is true.</summary>
    public class TakewhileIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, bool> _predicate;
        private bool _exhausted;

        internal TakewhileIterator(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            _enumerator = iterable.GetEnumerator();
            _predicate = predicate;
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

            if (!_enumerator.MoveNext())
            {
                _exhausted = true;
                _current = default;
                return false;
            }

            if (!_predicate(_enumerator.Current))
            {
                _exhausted = true;
                _current = default;
                return false;
            }

            _current = _enumerator.Current;
            return true;
        }
    }
}
