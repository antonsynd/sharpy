using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    public static partial class Itertools
    {
        public static ProductIterator<T> Product<T>(params IEnumerable<T>[] iterables)
        {
            return new ProductIterator<T>(iterables);
        }
    }

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
}
