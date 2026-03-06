using System;
using System.Collections.Generic;

namespace Sharpy
{
    internal static partial class Itertools
    {
        public static ZipLongestIterator<T> ZipLongest<T>(IEnumerable<T>[] iterables, T fillvalue = default!)
        {
            return new ZipLongestIterator<T>(iterables, fillvalue);
        }
    }

    public class ZipLongestIterator<T> : Iterator<T[]>
    {
        private readonly IEnumerator<T>[] _enumerators;
        private readonly T _fillvalue;
        private readonly bool[] _exhausted;
        private bool _allExhausted;

        internal ZipLongestIterator(IEnumerable<T>[] iterables, T fillvalue)
        {
            _enumerators = new IEnumerator<T>[iterables.Length];
            _exhausted = new bool[iterables.Length];
            for (int i = 0; i < iterables.Length; i++)
            {
                _enumerators[i] = iterables[i].GetEnumerator();
            }

            _fillvalue = fillvalue;
            _allExhausted = false;
        }

        public override bool MoveNext()
        {
            if (_allExhausted)
            {
                _current = default;
                return false;
            }

            var result = new T[_enumerators.Length];
            bool anyAdvanced = false;

            for (int i = 0; i < _enumerators.Length; i++)
            {
                if (_exhausted[i])
                {
                    result[i] = _fillvalue;
                }
                else if (_enumerators[i].MoveNext())
                {
                    result[i] = _enumerators[i].Current;
                    anyAdvanced = true;
                }
                else
                {
                    _exhausted[i] = true;
                    result[i] = _fillvalue;
                }
            }

            if (!anyAdvanced)
            {
                _allExhausted = true;
                _current = default;
                return false;
            }

            _current = result;
            return true;
        }
    }
}
