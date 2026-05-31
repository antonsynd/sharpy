using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// Return successive r-length combinations of elements in the iterable.
    /// </summary>
    public class CombinationsIterator<T> : Iterator<T[]>
    {
        private readonly T[] _pool;
        private readonly int _r;
        private readonly int[] _indices;
        private bool _started;
        private bool _exhausted;

        /// <summary>Create a combinations iterator with the given r-length.</summary>
        public CombinationsIterator(IEnumerable<T> iterable, int r)
        {
            _pool = iterable.ToArray();
            _r = r;

            if (r < 0)
            {
                throw new ValueError("r must be non-negative");
            }

            if (r > _pool.Length)
            {
                _exhausted = true;
                // Assign Array.Empty<int>() as a safe, never-accessed placeholder.
                // _indices is never used when _exhausted is true, so we avoid nullability.
                _indices = Array.Empty<int>();
            }
            else
            {
                _indices = Enumerable.Range(0, r).ToArray();
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

            // Find the rightmost index that can be incremented
            int i = _r - 1;
            while (i >= 0 && _indices[i] == _pool.Length - _r + i)
            {
                i--;
            }

            if (i < 0)
            {
                _exhausted = true;
                _current = default;
                return false;
            }

            _indices[i]++;
            for (int j = i + 1; j < _r; j++)
            {
                _indices[j] = _indices[j - 1] + 1;
            }

            _current = _indices.Select(idx => _pool[idx]).ToArray();
            return true;
        }
    }

    /// <summary>
    /// Return successive r-length permutations of elements in the iterable.
    /// </summary>
    public class PermutationsIterator<T> : Iterator<T[]>
    {
        private readonly T[] _pool;
        private readonly int _r;
        private readonly int[] _indices;
        private readonly int[] _cycles;
        private bool _started;
        private bool _exhausted;

        /// <summary>Create a permutations iterator with the given r-length.</summary>
        public PermutationsIterator(IEnumerable<T> iterable, int? r = null)
        {
            _pool = iterable.ToArray();
            _r = r ?? _pool.Length;

            if (_r < 0)
            {
                throw new ValueError("r must be non-negative");
            }

            if (_r > _pool.Length)
            {
                _exhausted = true;
                _indices = Array.Empty<int>();
                _cycles = Array.Empty<int>();
            }
            else
            {
                _indices = Enumerable.Range(0, _pool.Length).ToArray();
                _cycles = Enumerable.Range(_pool.Length - _r + 1, _r).Reverse().ToArray();
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
                _current = _indices.Take(_r).Select(i => _pool[i]).ToArray();
                return true;
            }

            for (int i = _r - 1; i >= 0; i--)
            {
                _cycles[i]--;
                if (_cycles[i] == 0)
                {
                    // Rotate indices
                    int temp = _indices[i];
                    Array.Copy(_indices, i + 1, _indices, i, _pool.Length - i - 1);
                    _indices[^1] = temp;
                    _cycles[i] = _pool.Length - i;
                }
                else
                {
                    int j = _pool.Length - _cycles[i];
                    (_indices[i], _indices[j]) = (_indices[j], _indices[i]);
                    _current = _indices.Take(_r).Select(idx => _pool[idx]).ToArray();
                    return true;
                }
            }

            _exhausted = true;
            _current = default;
            return false;
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

    /// <summary>Iterator that yields tuples from the Cartesian product of input iterables.</summary>
    public class ProductIterator<T> : Iterator<T[]>
    {
        private readonly T[][] _pools;
        private readonly int[] _indices;
        private bool _started;
        private bool _exhausted;

        internal ProductIterator(IEnumerable<T>[] iterables)
        {
            _pools = iterables.Select(it => it.ToArray()).ToArray();
            _indices = new int[_pools.Length];
            _started = false;
            _exhausted = false;

            // If any pool is empty, the product is empty
            for (int i = 0; i < _pools.Length; i++)
            {
                if (_pools[i].Length == 0)
                {
                    _exhausted = true;
                    break;
                }
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
                _current = GetCurrentTuple();
                return true;
            }

            // Increment indices from the rightmost position
            for (int i = _pools.Length - 1; i >= 0; i--)
            {
                _indices[i]++;
                if (_indices[i] < _pools[i].Length)
                {
                    _current = GetCurrentTuple();
                    return true;
                }

                _indices[i] = 0;
            }

            _exhausted = true;
            _current = default;
            return false;
        }

        private T[] GetCurrentTuple()
        {
            var result = new T[_pools.Length];
            for (int i = 0; i < _pools.Length; i++)
            {
                result[i] = _pools[i][_indices[i]];
            }

            return result;
        }
    }

    /// <summary>Functions creating iterators for efficient looping.</summary>
    public static partial class Itertools
    {
        /// <summary>
        /// Return r-length combinations of elements in the iterable.
        /// </summary>
        public static CombinationsIterator<T> Combinations<T>(IEnumerable<T> iterable, int r)
        {
            return new CombinationsIterator<T>(iterable, r);
        }

        /// <summary>
        /// Return successive r-length permutations of elements in the iterable.
        /// </summary>
        public static PermutationsIterator<T> Permutations<T>(IEnumerable<T> iterable, int? r = null)
        {
            return new PermutationsIterator<T>(iterable, r);
        }

        /// <summary>Return r-length combinations of elements allowing individual elements to be repeated.</summary>
        public static CombinationsWithReplacementIterator<T> CombinationsWithReplacement<T>(IEnumerable<T> iterable, int r)
        {
            return new CombinationsWithReplacementIterator<T>(iterable, r);
        }

        /// <summary>Cartesian product of input iterables, equivalent to nested for-loops.</summary>
        public static ProductIterator<T> Product<T>(params IEnumerable<T>[] iterables)
        {
            return new ProductIterator<T>(iterables);
        }
    }
}
