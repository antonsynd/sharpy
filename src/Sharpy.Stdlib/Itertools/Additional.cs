using System;
using System.Collections.Generic;
using System.Linq;
namespace Sharpy
{

    /// <summary>
    /// Make an iterator that returns elements from the first iterable until it is exhausted,
    /// then proceeds to the next iterable, until all of the iterables are exhausted.
    /// </summary>
    public class ChainIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<IEnumerable<T>> _iterables;
        private IEnumerator<T>? _currentIterator;

        /// <summary>Create a chain iterator over the given iterables.</summary>
        public ChainIterator(IEnumerable<IEnumerable<T>> iterables)
        {
            _iterables = iterables.GetEnumerator();
            _currentIterator = null;
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            while (true)
            {
                if (_currentIterator != null)
                {
                    if (_currentIterator.MoveNext())
                    {
                        _current = _currentIterator.Current;
                        return true;
                    }
                    else
                    {
                        _currentIterator = null;
                    }
                }

                if (!_iterables.MoveNext())
                {
                    _current = default;
                    return false;
                }

                _currentIterator = _iterables.Current.GetEnumerator();
            }
        }
    }

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

    public static partial class Itertools
    {
        /// <summary>
        /// Make an iterator that returns elements from the first iterable until it is exhausted,
        /// then proceeds to the next iterable.
        /// </summary>
        /// <param name="iterables">One or more iterables to chain together.</param>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>An iterator over the concatenated elements.</returns>
        /// <example>
        /// <code>
        /// list(itertools.chain([1, 2], [3, 4]))    # [1, 2, 3, 4]
        /// </code>
        /// </example>
        public static ChainIterator<T> Chain<T>(params IEnumerable<T>[] iterables)
        {
            return new ChainIterator<T>(iterables);
        }

        internal static IEnumerable<T> Islice<T>(IEnumerable<T> iterable, int stop)
        {
            return Islice(new Sharpy.List<T>(iterable), stop);
        }

        internal static IEnumerable<T> Islice<T>(IEnumerable<T> iterable, int start, int stop, int step = 1)
        {
            return IsliceRange(new Sharpy.List<T>(iterable), start, stop, step);
        }

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
    }
}
