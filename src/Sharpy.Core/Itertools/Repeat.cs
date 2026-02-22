
namespace Sharpy
{
    internal static partial class Itertools
    {
        public static Iterator<T> Repeat<T>(T elem)
        {
            return new RepeatIterator<T>(elem);
        }

        public static Iterator<T> Repeat<T>(T elem, uint n)
        {
            return new RepeatIterator<T>(elem, n);
        }
    }

    internal class RepeatIterator<T> : Iterator<T>
    {
        private readonly T _elem;
        private readonly bool _infinite;
        private bool _active;
        private uint _n;

        internal RepeatIterator(T elem)
        {
            _elem = elem;
            _infinite = true;
            _active = true;
            _n = 0;
        }

        internal RepeatIterator(T elem, uint n)
        {
            _elem = elem;
            _infinite = false;
            _active = true;
            _n = n;
        }

        public override bool MoveNext()
        {
            if (_infinite)
            {
                _current = _elem;
                return true;
            }

            if (_active)
            {
                if (_n > 0)
                {
                    --_n;
                    _current = _elem;
                    return true;
                }

                _active = false;
                _current = _elem;
                return true;
            }

            _current = default;
            return false;
        }
    }
}
