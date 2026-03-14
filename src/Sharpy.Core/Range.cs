namespace Sharpy
{
    /// <summary>
    /// Iterator that yields integers in a range.
    /// </summary>
    public class RangeIterator : Iterator<int>
    {
        private readonly int _start;
        private readonly int _stop;
        private readonly int _step;
        private int _position;

        /// <summary>
        /// Initializes a new instance of the <see cref="RangeIterator"/> class.
        /// </summary>
        /// <param name="start">The starting value</param>
        /// <param name="stop">The stopping value (exclusive)</param>
        /// <param name="step">The step value</param>
        public RangeIterator(int start, int stop, int step)
        {
            if (step == 0)
            {
                throw new ValueError("range() step argument must not be zero");
            }

            _start = start;
            _stop = stop;
            _step = step;
            _position = start;
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (_step > 0)
            {
                if (_position >= _stop)
                {
                    _current = default;
                    return false;
                }
            }
            else
            {
                if (_position <= _stop)
                {
                    _current = default;
                    return false;
                }
            }

            _current = _position;
            _position += _step;
            return true;
        }
    }

    public static partial class Builtins
    {
        /// <summary>
        /// Return an iterator that produces integers from 0 up to (but not including) stop.
        /// </summary>
        /// <param name="stop">The stopping value (exclusive)</param>
        /// <returns>A range iterator</returns>
        /// <example>
        /// <code>
        /// list(range(5))         # [0, 1, 2, 3, 4]
        /// list(range(2, 5))      # [2, 3, 4]
        /// list(range(0, 10, 2))  # [0, 2, 4, 6, 8]
        /// </code>
        /// </example>
        public static RangeIterator Range(int stop)
        {
            return new RangeIterator(0, stop, 1);
        }

        /// <summary>
        /// Return an iterator that produces integers from start up to (but not including) stop.
        /// </summary>
        /// <param name="start">The starting value</param>
        /// <param name="stop">The stopping value (exclusive)</param>
        /// <returns>A range iterator</returns>
        public static RangeIterator Range(int start, int stop)
        {
            return new RangeIterator(start, stop, 1);
        }

        /// <summary>
        /// Return an iterator that produces integers from start up to (but not including) stop,
        /// incrementing by step.
        /// </summary>
        /// <param name="start">The starting value</param>
        /// <param name="stop">The stopping value (exclusive)</param>
        /// <param name="step">The step value</param>
        /// <returns>A range iterator</returns>
        /// <exception cref="ValueError">Thrown when <paramref name="step"/> is zero</exception>
        public static RangeIterator Range(int start, int stop, int step)
        {
            return new RangeIterator(start, stop, step);
        }
    }
}
