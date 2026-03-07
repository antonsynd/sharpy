using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    // --- Iterator classes ---

    /// <summary>
    /// Make an iterator that returns evenly spaced values starting with start.
    /// </summary>
    public class CountIterator : Iterator<long>
    {
        private long _n;
        private readonly long _step;

        public CountIterator(long start, long step)
        {
            _n = start;
            _step = step;
        }

        public override bool MoveNext()
        {
            _current = _n;
            _n += _step;
            return true;
        }
    }

    /// <summary>
    /// Make an iterator that returns accumulated sums (or other binary function results).
    /// </summary>
    public class AccumulateIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, T, T> _func;
        private T? _total;
        private bool _started;
        private readonly bool _hasInitial;
        private readonly T? _initial;

        public AccumulateIterator(IEnumerable<T> iterable, Func<T, T, T> func, T? initial, bool hasInitial)
        {
            _enumerator = iterable.GetEnumerator();
            _func = func;
            _hasInitial = hasInitial;
            _initial = initial;
            _started = false;
        }

        public override bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
                if (_hasInitial)
                {
                    _total = _initial;
                    _current = _total;
                    return true;
                }

                if (!_enumerator.MoveNext())
                {
                    _current = default;
                    return false;
                }

                _total = _enumerator.Current;
                _current = _total;
                return true;
            }

            if (!_enumerator.MoveNext())
            {
                _current = default;
                return false;
            }

            _total = _func(_total!, _enumerator.Current);
            _current = _total;
            return true;
        }
    }

    /// <summary>
    /// Make an iterator that drops elements from the iterable as long as the predicate is true.
    /// </summary>
    public class DropwhileIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, bool> _predicate;
        private bool _dropping;

        public DropwhileIterator(IEnumerable<T> iterable, Func<T, bool> predicate)
        {
            _enumerator = iterable.GetEnumerator();
            _predicate = predicate;
            _dropping = true;
        }

        public override bool MoveNext()
        {
            while (_enumerator.MoveNext())
            {
                if (_dropping)
                {
                    if (_predicate(_enumerator.Current))
                    {
                        continue;
                    }

                    _dropping = false;
                }

                _current = _enumerator.Current;
                return true;
            }

            _current = default;
            return false;
        }
    }

    /// <summary>
    /// Make an iterator that returns elements from the iterable as long as the predicate is true.
    /// </summary>
    public class TakewhileIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, bool> _predicate;
        private bool _done;

        public TakewhileIterator(IEnumerable<T> iterable, Func<T, bool> predicate)
        {
            _enumerator = iterable.GetEnumerator();
            _predicate = predicate;
            _done = false;
        }

        public override bool MoveNext()
        {
            if (_done)
            {
                _current = default;
                return false;
            }

            if (!_enumerator.MoveNext())
            {
                _current = default;
                return false;
            }

            if (_predicate(_enumerator.Current))
            {
                _current = _enumerator.Current;
                return true;
            }

            _done = true;
            _current = default;
            return false;
        }
    }

    /// <summary>
    /// Make an iterator that filters elements from data returning only those that have a corresponding
    /// element in selectors that evaluates to True.
    /// </summary>
    public class CompressIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _data;
        private readonly IEnumerator<bool> _selectors;

        public CompressIterator(IEnumerable<T> data, IEnumerable<bool> selectors)
        {
            _data = data.GetEnumerator();
            _selectors = selectors.GetEnumerator();
        }

        public override bool MoveNext()
        {
            while (_data.MoveNext() && _selectors.MoveNext())
            {
                if (_selectors.Current)
                {
                    _current = _data.Current;
                    return true;
                }
            }

            _current = default;
            return false;
        }
    }

    /// <summary>
    /// Make an iterator that filters elements from iterable returning only those for which the predicate is False.
    /// </summary>
    public class FilterfalseIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, bool> _predicate;

        public FilterfalseIterator(IEnumerable<T> iterable, Func<T, bool> predicate)
        {
            _enumerator = iterable.GetEnumerator();
            _predicate = predicate;
        }

        public override bool MoveNext()
        {
            while (_enumerator.MoveNext())
            {
                if (!_predicate(_enumerator.Current))
                {
                    _current = _enumerator.Current;
                    return true;
                }
            }

            _current = default;
            return false;
        }
    }

    /// <summary>
    /// Make an iterator that computes the function using arguments obtained from the iterable.
    /// </summary>
    public class StarmapIterator<T1, T2, TResult> : Iterator<TResult>
    {
        private readonly IEnumerator<(T1, T2)> _enumerator;
        private readonly Func<T1, T2, TResult> _func;

        public StarmapIterator(IEnumerable<(T1, T2)> iterable, Func<T1, T2, TResult> func)
        {
            _enumerator = iterable.GetEnumerator();
            _func = func;
        }

        public override bool MoveNext()
        {
            if (!_enumerator.MoveNext())
            {
                _current = default;
                return false;
            }

            var (a, b) = _enumerator.Current;
            _current = _func(a, b);
            return true;
        }
    }

    /// <summary>
    /// Make an iterator that aggregates elements from each of the iterables.
    /// If the iterables are of uneven length, missing values are filled-in with fillvalue.
    /// </summary>
    public class ZipLongestIterator<T> : Iterator<T[]>
    {
        private readonly IEnumerator<T>[] _enumerators;
        private readonly T _fillvalue;
        private readonly bool[] _exhausted;

        public ZipLongestIterator(IEnumerable<T>[] iterables, T fillvalue)
        {
            _enumerators = new IEnumerator<T>[iterables.Length];
            for (int i = 0; i < iterables.Length; i++)
            {
                _enumerators[i] = iterables[i].GetEnumerator();
            }

            _fillvalue = fillvalue;
            _exhausted = new bool[iterables.Length];
        }

        public override bool MoveNext()
        {
            bool anyAlive = false;
            T[] result = new T[_enumerators.Length];

            for (int i = 0; i < _enumerators.Length; i++)
            {
                if (_exhausted[i])
                {
                    result[i] = _fillvalue;
                }
                else if (_enumerators[i].MoveNext())
                {
                    result[i] = _enumerators[i].Current;
                    anyAlive = true;
                }
                else
                {
                    _exhausted[i] = true;
                    result[i] = _fillvalue;
                }
            }

            if (!anyAlive)
            {
                _current = default;
                return false;
            }

            _current = result;
            return true;
        }
    }

    /// <summary>
    /// Cartesian product of input iterables.
    /// </summary>
    public class ProductIterator<T> : Iterator<T[]>
    {
        private readonly T[][] _pools;
        private readonly int[] _indices;
        private bool _started;
        private bool _exhausted;

        public ProductIterator(T[][] pools)
        {
            _pools = pools;
            if (_pools.Length == 0 || Array.Exists(_pools, p => p.Length == 0))
            {
                _exhausted = true;
                _indices = Array.Empty<int>();
            }
            else
            {
                _indices = new int[_pools.Length];
                _exhausted = false;
            }

            _started = false;
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
                _current = CurrentTuple();
                return true;
            }

            // Increment from the rightmost index
            for (int i = _indices.Length - 1; i >= 0; i--)
            {
                _indices[i]++;
                if (_indices[i] < _pools[i].Length)
                {
                    _current = CurrentTuple();
                    return true;
                }

                _indices[i] = 0;
            }

            _exhausted = true;
            _current = default;
            return false;
        }

        private T[] CurrentTuple()
        {
            T[] result = new T[_pools.Length];
            for (int i = 0; i < _pools.Length; i++)
            {
                result[i] = _pools[i][_indices[i]];
            }

            return result;
        }
    }

    /// <summary>
    /// Make an iterator that returns consecutive keys and groups from the iterable.
    /// </summary>
    public class GroupbyIterator<T, TKey> : Iterator<(TKey Key, Iterator<T> Group)>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, TKey> _keyFunc;
        private bool _hasNext;
        private T? _currentItem;
        private TKey? _currentKey;

        public GroupbyIterator(IEnumerable<T> iterable, Func<T, TKey> keyFunc)
        {
            _enumerator = iterable.GetEnumerator();
            _keyFunc = keyFunc;
            _hasNext = _enumerator.MoveNext();
            if (_hasNext)
            {
                _currentItem = _enumerator.Current;
                _currentKey = _keyFunc(_currentItem);
            }
        }

        public override bool MoveNext()
        {
            if (!_hasNext)
            {
                _current = default;
                return false;
            }

            TKey key = _currentKey!;
            var groupItems = new System.Collections.Generic.List<T>();

            while (_hasNext && EqualityComparer<TKey>.Default.Equals(_currentKey, key))
            {
                groupItems.Add(_currentItem!);
                _hasNext = _enumerator.MoveNext();
                if (_hasNext)
                {
                    _currentItem = _enumerator.Current;
                    _currentKey = _keyFunc(_currentItem);
                }
            }

            _current = (key, new GroupListIterator<T>(groupItems));
            return true;
        }
    }

    /// <summary>
    /// Simple iterator that wraps a list, used by groupby.
    /// </summary>
    internal class GroupListIterator<T> : Iterator<T>
    {
        private readonly System.Collections.Generic.IList<T> _items;
        private int _index;

        internal GroupListIterator(System.Collections.Generic.IList<T> items)
        {
            _items = items;
            _index = 0;
        }

        public override bool MoveNext()
        {
            if (_index < _items.Count)
            {
                _current = _items[_index];
                _index++;
                return true;
            }

            _current = default;
            return false;
        }
    }

    /// <summary>
    /// Return r-length subsequences of elements from the input iterable allowing individual elements to be repeated.
    /// </summary>
    public class CombinationsWithReplacementIterator<T> : Iterator<T[]>
    {
        private readonly T[] _pool;
        private readonly int _r;
        private readonly int[] _indices;
        private bool _started;
        private bool _exhausted;

        public CombinationsWithReplacementIterator(IEnumerable<T> iterable, int r)
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
                _indices = Array.Empty<int>();
            }
            else
            {
                _indices = new int[r]; // all start at 0
                _exhausted = false;
            }

            _started = false;
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
                _current = _indices.Select(i => _pool[i]).ToArray();
                return true;
            }

            // Find rightmost index that can be incremented
            int i = _r - 1;
            while (i >= 0 && _indices[i] == _pool.Length - 1)
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

    /// <summary>
    /// Return successive overlapping pairs taken from the input iterable.
    /// </summary>
    public class PairwiseIterator<T> : Iterator<(T First, T Second)>
    {
        private readonly IEnumerator<T> _enumerator;
        private T? _prev;
        private bool _started;

        public PairwiseIterator(IEnumerable<T> iterable)
        {
            _enumerator = iterable.GetEnumerator();
            _started = false;
        }

        public override bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
                if (!_enumerator.MoveNext())
                {
                    _current = default;
                    return false;
                }

                _prev = _enumerator.Current;
            }

            if (!_enumerator.MoveNext())
            {
                _current = default;
                return false;
            }

            _current = (_prev!, _enumerator.Current);
            _prev = _enumerator.Current;
            return true;
        }
    }

    // --- Module methods ---

    public static partial class Itertools
    {
        /// <summary>
        /// Make an iterator that returns evenly spaced values starting with start.
        /// </summary>
        public static CountIterator Count(long start = 0, long step = 1)
        {
            return new CountIterator(start, step);
        }

        /// <summary>
        /// Make an iterator that returns accumulated sums.
        /// </summary>
        public static AccumulateIterator<T> Accumulate<T>(IEnumerable<T> iterable, Func<T, T, T> func)
        {
            return new AccumulateIterator<T>(iterable, func, default, false);
        }

        /// <summary>
        /// Make an iterator that returns accumulated sums with an initial value.
        /// </summary>
        public static AccumulateIterator<T> Accumulate<T>(IEnumerable<T> iterable, Func<T, T, T> func, T initial)
        {
            return new AccumulateIterator<T>(iterable, func, initial, true);
        }

        /// <summary>
        /// Make an iterator that drops elements from the iterable as long as the predicate is true.
        /// </summary>
        public static DropwhileIterator<T> Dropwhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return new DropwhileIterator<T>(iterable, predicate);
        }

        /// <summary>
        /// Make an iterator that returns elements from the iterable as long as the predicate is true.
        /// </summary>
        public static TakewhileIterator<T> Takewhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return new TakewhileIterator<T>(iterable, predicate);
        }

        /// <summary>
        /// Make an iterator that filters elements from data returning only those that have
        /// a corresponding element in selectors that evaluates to True.
        /// </summary>
        public static CompressIterator<T> Compress<T>(IEnumerable<T> data, IEnumerable<bool> selectors)
        {
            return new CompressIterator<T>(data, selectors);
        }

        /// <summary>
        /// Make an iterator that filters elements from iterable returning only those for which
        /// the predicate is False.
        /// </summary>
        public static FilterfalseIterator<T> Filterfalse<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return new FilterfalseIterator<T>(iterable, predicate);
        }

        /// <summary>
        /// Make an iterator that computes the function using arguments obtained from the iterable.
        /// </summary>
        public static StarmapIterator<T1, T2, TResult> Starmap<T1, T2, TResult>(
            Func<T1, T2, TResult> func, IEnumerable<(T1, T2)> iterable)
        {
            return new StarmapIterator<T1, T2, TResult>(iterable, func);
        }

        /// <summary>
        /// Make an iterator that aggregates elements from each of the iterables.
        /// If the iterables are of uneven length, missing values are filled-in with fillvalue.
        /// </summary>
        public static ZipLongestIterator<T> ZipLongest<T>(T fillvalue, params IEnumerable<T>[] iterables)
        {
            return new ZipLongestIterator<T>(iterables, fillvalue);
        }

        /// <summary>
        /// Cartesian product of input iterables.
        /// </summary>
        public static ProductIterator<T> Product<T>(params IEnumerable<T>[] iterables)
        {
            T[][] pools = new T[iterables.Length][];
            for (int i = 0; i < iterables.Length; i++)
            {
                pools[i] = iterables[i].ToArray();
            }

            return new ProductIterator<T>(pools);
        }

        /// <summary>
        /// Cartesian product of a single iterable repeated 'repeat' times.
        /// </summary>
        public static ProductIterator<T> Product<T>(IEnumerable<T> iterable, int repeat)
        {
            T[] pool = iterable.ToArray();
            T[][] pools = new T[repeat][];
            for (int i = 0; i < repeat; i++)
            {
                pools[i] = pool;
            }

            return new ProductIterator<T>(pools);
        }

        /// <summary>
        /// Make an iterator that returns consecutive keys and groups from the iterable.
        /// </summary>
        public static GroupbyIterator<T, TKey> Groupby<T, TKey>(
            IEnumerable<T> iterable, Func<T, TKey> key)
        {
            return new GroupbyIterator<T, TKey>(iterable, key);
        }

        /// <summary>
        /// Make an iterator that returns consecutive groups using identity as the key function.
        /// </summary>
        public static GroupbyIterator<T, T> Groupby<T>(IEnumerable<T> iterable)
        {
            return new GroupbyIterator<T, T>(iterable, x => x);
        }

        /// <summary>
        /// Return r-length subsequences of elements allowing individual elements to be repeated.
        /// </summary>
        public static CombinationsWithReplacementIterator<T> CombinationsWithReplacement<T>(
            IEnumerable<T> iterable, int r)
        {
            return new CombinationsWithReplacementIterator<T>(iterable, r);
        }

        /// <summary>
        /// Return successive overlapping pairs taken from the input iterable.
        /// </summary>
        public static PairwiseIterator<T> Pairwise<T>(IEnumerable<T> iterable)
        {
            return new PairwiseIterator<T>(iterable);
        }
    }
}
