using System;
using System.Collections.Generic;

namespace Sharpy
{
    internal static partial class Itertools
    {
        public static GroupbyIterator<T, TKey> Groupby<T, TKey>(IEnumerable<T> iterable, Func<T, TKey>? key = null)
        {
            return new GroupbyIterator<T, TKey>(iterable, key ?? (x => (TKey)(object)x!));
        }
    }

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
}
