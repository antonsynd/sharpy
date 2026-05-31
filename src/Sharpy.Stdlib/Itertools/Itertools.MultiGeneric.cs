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

    /// <summary>Iterator that groups consecutive elements by key.</summary>
    public class GroupbyIterator<T, TKey> : Iterator<(TKey Key, Sharpy.List<T> Group)>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, TKey> _keyFunc;
        private bool _exhausted;
        private bool _hasBuffered;
        private T? _buffered;

        internal GroupbyIterator(IEnumerable<T> iterable, Func<T, TKey> keyFunc)
        {
            _enumerator = iterable.GetEnumerator();
            _keyFunc = keyFunc;
            _exhausted = false;
            _hasBuffered = false;
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (_exhausted)
            {
                _current = default;
                return false;
            }

            T item;
            TKey currentKey;

            if (_hasBuffered)
            {
                item = _buffered!;
                currentKey = _keyFunc(item);
                _hasBuffered = false;
            }
            else
            {
                if (!_enumerator.MoveNext())
                {
                    _exhausted = true;
                    _current = default;
                    return false;
                }

                item = _enumerator.Current;
                currentKey = _keyFunc(item);
            }

            var group = new Sharpy.List<T>();
            group.Append(item);

            while (_enumerator.MoveNext())
            {
                var nextKey = _keyFunc(_enumerator.Current);
                if (EqualityComparer<TKey>.Default.Equals(nextKey, currentKey))
                {
                    group.Append(_enumerator.Current);
                }
                else
                {
                    _buffered = _enumerator.Current;
                    _hasBuffered = true;
                    break;
                }
            }

            if (!_hasBuffered && !_enumerator.MoveNext())
            {
                // Don't set exhausted here; we'll discover it on the next call
            }

            _current = (currentKey, group);
            return true;
        }
    }

    /// <summary>Functions creating iterators for efficient looping.</summary>
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

        /// <summary>Make an iterator that returns consecutive keys and groups from the iterable.</summary>
        public static GroupbyIterator<T, TKey> Groupby<T, TKey>(IEnumerable<T> iterable, Func<T, TKey>? key = null)
        {
            return new GroupbyIterator<T, TKey>(iterable, key ?? (x => (TKey)(object)x!));
        }
    }
}
