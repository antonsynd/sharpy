namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for read-only sequences.
/// </summary>
public interface ISequence<T>
    : ICollection<T>,
      IReversible<T>,
      IAddable<ISequence<T>>,
      IRightAddable<ISequence<T>>
{
    /// <summary>
    /// Returns the item at the given index in the sequence. Supports negative
    /// indices to get or set from the back. If the index is out of range,
    /// then this raises an <see cref="IndexError"/>.
    /// </summary>
    T this[int index]
    {
        get
        {
            return __GetItem__((int)Sharpy.Index.Normalize(index, __Len__(), false, false));
        }
    }

    T this[System.Index index]
    {
        get
        {
            return __GetItem__((int)index.ToNormalizedUint32(__Len__(), false, false));
        }
    }

    ISequence<T> this[System.Range range]
    {
        get
        {
            return __GetItem__(range.ToSlice(__Len__(), false));
        }
    }

    ISequence<T> this[Slice slice]
    {
        get
        {
            return __GetItem__(slice);
        }
    }

    /// <summary>
    /// Returns the sub-sequence at the given start and end (exclusive) indices
    /// in the sequence. Supports negative indices to count from the back.
    /// If either index is out of range, then this raises an
    /// <see cref="IndexError"/>.
    /// </summary>
    /// <remarks>
    /// Should call <see cref="__GetItem__(Slice)"/> underneath.
    /// </remarks>
    ISequence<T> this[int start, int end]
    {
        get
        {
            return __GetItem__(new Slice(start, end));
        }
    }

    /// <summary>
    /// Returns the sub-sequence at the given start and end (exclusive)
    /// indices in the sequence, skipping every step elements after the
    /// start. Supports negative indices to count from the back. If either
    /// index is out of range, then this raises an
    /// <see cref="IndexError"/>. If the step is less than 1, then an
    /// empty sequence is returned.
    /// </summary>
    /// <remarks>
    /// Should call <see cref="__GetItem__(Slice)"/> underneath.
    /// </remarks>
    ISequence<T> this[int start, int end, int step]
    {
        get
        {
            return __GetItem__(new Slice(start, end, step));
        }
    }

    /// <summary>
    /// Returns the number of times the given element appears in the
    /// sequence as evaluated by Sharpy's equality resolution.
    /// </summary>
    new uint Count(T x);

    /// <summary>
    /// Returns the index of the first occurrence of the given element in
    /// the sequence as evaluated by Sharpy's equality resolution, within
    /// the given start and end (exclusive) indices. Supports negative
    /// indices to count from the back.
    /// </summary>
    uint Index(T x, int start = 0, int end = -1);

    /// <summary>
    /// Returns the element at the given index in the sequence. Supports
    /// negative indices to get or set from the back. If the index is out
    /// of range, then this raises an <see cref="IndexError"/>.
    /// </summary>
    T __GetItem__(int index);

    /// <summary>
    /// Returns a (sub-)sequence for the given slice. If any component of
    /// the slice is out of range, then this raises an
    /// <see cref="IndexError"/>.
    /// </summary>
    ISequence<T> __GetItem__(Slice slice);
}

/// <summary>
/// Interface for read-only sequences as a curiously recursive template,
/// with methods dealing directly with the given sequence type.
/// </summary>
public interface ISequence<TSequence, TItem> : ISequence<TItem>
{
    new TSequence this[System.Range range]
    {
        get
        {
            return __GetItem__(range.ToSlice(__Len__(), false));
        }
    }

    new TSequence this[Slice slice]
    {
        get
        {
            return __GetItem__(slice);
        }
    }

    /// <summary>
    /// Returns the sub-sequence at the given start and end (exclusive) indices
    /// in the sequence. Supports negative indices to count from the back.
    /// If either index is out of range, then this raises an
    /// <see cref="IndexError"/>.
    /// </summary>
    /// <remarks>
    /// Should call <see cref="__GetItem__(Slice)"/> underneath.
    /// </remarks>
    new TSequence this[int start, int end]
    {
        get
        {
            return __GetItem__(new Slice(start, end));
        }
    }

    /// <summary>
    /// Returns the sub-sequence at the given start and end (exclusive)
    /// indices in the sequence, skipping every step elements after the
    /// start. Supports negative indices to count from the back. If either
    /// index is out of range, then this raises an
    /// <see cref="IndexError"/>. If the step is less than 1, then an
    /// empty sequence is returned.
    /// </summary>
    /// <remarks>
    /// Should call <see cref="__GetItem__(Slice)"/> underneath.
    /// </remarks>
    new TSequence this[int start, int end, int step]
    {
        get
        {
            return __GetItem__(new Slice(start, end, step));
        }
    }

    /// <summary>
    /// Returns the sub-sequence at the given start and end (exclusive)
    /// indices in the sequence, skipping every step elements after the
    /// start. Supports negative indices to count from the back. If either
    /// index is out of range, then this raises an
    /// <see cref="IndexError"/>. If the step is less than 1, then an
    /// empty sequence is returned.
    /// </summary>
    new TSequence __GetItem__(Slice slice);
}
