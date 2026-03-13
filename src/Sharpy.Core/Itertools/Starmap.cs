using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Make an iterator that computes the function using arguments obtained from the iterable.
    /// </summary>
    public class StarmapIterator<T1, T2, TResult> : Iterator<TResult>
    {
        private readonly IEnumerator<(T1, T2)> _enumerator;
        private readonly Func<T1, T2, TResult> _func;

        /// <summary>Create a starmap iterator.</summary>
        public StarmapIterator(IEnumerable<(T1, T2)> iterable, Func<T1, T2, TResult> func)
        {
            _enumerator = iterable.GetEnumerator();
            _func = func;
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (!_enumerator.MoveNext())
            {
                _current = default;
                return false;
            }

            var (a, b) = _enumerator.Current;
            _current = _func(a, b);
            return true;
        }
    }

    public static partial class Itertools
    {
        /// <summary>
        /// Make an iterator that computes the function using arguments obtained from the iterable.
        /// </summary>
        public static StarmapIterator<T1, T2, TResult> Starmap<T1, T2, TResult>(
            Func<T1, T2, TResult> func, IEnumerable<(T1, T2)> iterable)
        {
            return new StarmapIterator<T1, T2, TResult>(iterable, func);
        }
    }
}
