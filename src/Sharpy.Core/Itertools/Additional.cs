namespace Sharpy.Itertools;

using Sharpy.Core;

/// <summary>
/// Make an iterator that returns elements from the first iterable until it is exhausted,
/// then proceeds to the next iterable, until all of the iterables are exhausted.
/// </summary>
public class ChainIterator<T> : Iterator<T>
{
    private readonly IEnumerator<IEnumerable<T>> _iterables;
    private IEnumerator<T>? _currentIterator;

    public ChainIterator(IEnumerable<IEnumerable<T>> iterables)
    {
        _iterables = iterables.GetEnumerator();
        _currentIterator = null;
    }

    public override T __Next__()
    {
        while (true)
        {
            if (_currentIterator != null)
            {
                if (_currentIterator.MoveNext())
                {
                    return _currentIterator.Current;
                }
                else
                {
                    _currentIterator = null;
                }
            }

            if (!_iterables.MoveNext())
            {
                throw new StopIteration();
            }

            _currentIterator = _iterables.Current.GetEnumerator();
        }
    }
}

/// <summary>
/// Make an iterator that returns selected elements from the iterable.
/// Note: The constructor consumes elements from the underlying iterator to skip to the start position.
/// If the iterator is exhausted before reaching the start index, an empty iterator is created.
/// </summary>
public class IsliceIterator<T> : Iterator<T>
{
    private readonly IEnumerator<T> _enumerator;
    private readonly int _stop;
    private readonly int _step;
    private int _currentIndex;
    private bool _exhausted;

    public IsliceIterator(IEnumerable<T> iterable, int stop)
        : this(iterable, 0, stop, 1)
    {
    }

    public IsliceIterator(IEnumerable<T> iterable, int start, int stop, int step = 1)
    {
        if (start < 0 || stop < 0 || step <= 0)
        {
            throw new ValueError("Indices for islice() must be non-negative");
        }

        _enumerator = iterable.GetEnumerator();
        _stop = stop;
        _step = step;
        _currentIndex = 0;
        _exhausted = false;

        // Skip to start - if iterator is exhausted before start, mark as exhausted
        for (int i = 0; i < start; i++)
        {
            if (!_enumerator.MoveNext())
            {
                _exhausted = true;
                break;
            }
            _currentIndex++;
        }
    }

    public override T __Next__()
    {
        if (_exhausted || _currentIndex >= _stop)
        {
            throw new StopIteration();
        }

        if (!_enumerator.MoveNext())
        {
            _exhausted = true;
            throw new StopIteration();
        }

        T value = _enumerator.Current;
        _currentIndex++;

        // Skip step - 1 elements
        for (int i = 1; i < _step; i++)
        {
            if (!_enumerator.MoveNext())
            {
                _exhausted = true;
                break;
            }
            _currentIndex++;
        }

        return value;
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

    public override T[] __Next__()
    {
        if (_exhausted)
        {
            throw new StopIteration();
        }

        if (!_started)
        {
            _started = true;
            return _indices.Select(i => _pool[i]).ToArray();
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
            throw new StopIteration();
        }

        _indices[i]++;
        for (int j = i + 1; j < _r; j++)
        {
            _indices[j] = _indices[j - 1] + 1;
        }

        return _indices.Select(idx => _pool[idx]).ToArray();
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

    public override T[] __Next__()
    {
        if (_exhausted)
        {
            throw new StopIteration();
        }

        if (!_started)
        {
            _started = true;
            return _indices.Take(_r).Select(i => _pool[i]).ToArray();
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
                return _indices.Take(_r).Select(idx => _pool[idx]).ToArray();
            }
        }

        _exhausted = true;
        throw new StopIteration();
    }
}

internal static partial class Exports
{
    /// <summary>
    /// Make an iterator that returns elements from the first iterable until it is exhausted,
    /// then proceeds to the next iterable.
    /// </summary>
    public static ChainIterator<T> Chain<T>(params IEnumerable<T>[] iterables)
    {
        return new ChainIterator<T>(iterables);
    }

    /// <summary>
    /// Make an iterator that returns selected elements from the iterable.
    /// </summary>
    public static IsliceIterator<T> Islice<T>(IEnumerable<T> iterable, int stop)
    {
        return new IsliceIterator<T>(iterable, stop);
    }

    /// <summary>
    /// Make an iterator that returns selected elements from the iterable.
    /// </summary>
    public static IsliceIterator<T> Islice<T>(IEnumerable<T> iterable, int start, int stop, int step = 1)
    {
        return new IsliceIterator<T>(iterable, start, stop, step);
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
