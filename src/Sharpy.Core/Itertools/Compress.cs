using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Itertools
    {
        public static CompressIterator<T> Compress<T>(IEnumerable<T> data, IEnumerable<bool> selectors)
        {
            return new CompressIterator<T>(data, selectors);
        }
    }

    public class CompressIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _data;
        private readonly IEnumerator<bool> _selectors;
        private bool _exhausted;

        internal CompressIterator(IEnumerable<T> data, IEnumerable<bool> selectors)
        {
            _data = data.GetEnumerator();
            _selectors = selectors.GetEnumerator();
            _exhausted = false;
        }

        public override bool MoveNext()
        {
            if (_exhausted)
            {
                _current = default;
                return false;
            }

            while (_data.MoveNext() && _selectors.MoveNext())
            {
                if (_selectors.Current)
                {
                    _current = _data.Current;
                    return true;
                }
            }

            _exhausted = true;
            _current = default;
            return false;
        }
    }
}
