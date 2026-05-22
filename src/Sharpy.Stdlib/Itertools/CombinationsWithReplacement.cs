using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    public static partial class Itertools
    {
        /// <summary>Return r-length combinations of elements allowing individual elements to be repeated.</summary>
        public static CombinationsWithReplacementIterator<T> CombinationsWithReplacement<T>(IEnumerable<T> iterable, int r)
        {
            return new CombinationsWithReplacementIterator<T>(iterable, r);
        }
    }

    /// <summary>Iterator that yields r-length combinations with replacement.</summary>
    public class CombinationsWithReplacementIterator<T> : Iterator<T[]>
    {
        private readonly T[] _pool;
        private readonly int _r;
        private readonly int[] _indices;
        private bool _started;
        private bool _exhausted;

        internal CombinationsWithReplacementIterator(IEnumerable<T> iterable, int r)
        {
            _pool = iterable.ToArray();
            _r = r;

            if (r < 0)
            {
                throw new ValueError("r must be non-negative");
            }

            if (_pool.Length == 0 && r > 0)
            {
                _exhausted = true;
                _indices = System.Array.Empty<int>();
            }
            else
            {
                _indices = new int[r];
                _started = false;
                _exhausted = false;
            }
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (_exhausted)
            {
                _current = default;
                return false;
            }

            if (!_started)
            {
                _started = true;
                _current = _indices.Select(i => _pool[i]).ToArray();
                return true;
            }

            int n = _pool.Length;
            int i = _r - 1;
            while (i >= 0 && _indices[i] == n - 1)
            {
                i--;
            }

            if (i < 0)
            {
                _exhausted = true;
                _current = default;
                return false;
            }

            int newVal = _indices[i] + 1;
            for (int j = i; j < _r; j++)
            {
                _indices[j] = newVal;
            }

            _current = _indices.Select(idx => _pool[idx]).ToArray();
            return true;
        }
    }
}
