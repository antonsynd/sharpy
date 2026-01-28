namespace Sharpy.Core
{
    /// <summary>
    /// Iterator that yields integers in a range.
    /// </summary>
    public class RangeIterator : Iterator<int>
    {
        private readonly int _start;
        private readonly int _stop;
        private readonly int _step;
        private int _current;

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
            _current = start;
        }

        /// <inheritdoc/>
        public override int __Next__()
        {
            if (_step > 0)
            {
                if (_current >= _stop)
                {
                    throw new StopIteration();
                }
            }
            else
            {
                if (_current <= _stop)
                {
                    throw new StopIteration();
                }
            }

            var result = _current;
            _current += _step;
            return result;
        }
    }

    public static partial class Exports
    {
        /// <summary>
        /// Return an iterator that produces integers from 0 up to (but not including) stop.
        /// </summary>
        /// <param name="stop">The stopping value (exclusive)</param>
        /// <returns>A range iterator</returns>
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
        public static RangeIterator Range(int start, int stop, int step)
        {
            return new RangeIterator(start, stop, step);
        }
    }
}
