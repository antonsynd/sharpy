using System.Collections.Generic;

namespace Sharpy
{
    internal static partial class Itertools
    {
        public static CountIterator Count(int start = 0, int step = 1)
        {
            return new CountIterator(start, step);
        }
    }

    public class CountIterator : Iterator<int>
    {
        private readonly int _step;
        private int _value;
        private bool _started;

        internal CountIterator(int start, int step)
        {
            _value = start;
            _step = step;
            _started = false;
        }

        public override bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
                _current = _value;
                return true;
            }

            _value += _step;
            _current = _value;
            return true;
        }
    }
}
