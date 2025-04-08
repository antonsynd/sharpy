namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only sequences.
    /// </summary>
    public interface ISequence<T> : ICollection<T>, IReversible<T>, IAddable<ISequence<T>>, IRightAddable<ISequence<T>>
    {
        /// <summary>
        /// Returns the item at the given index in the sequence. Supports negative
        /// indices to get or set from the back. If the index is out of range,
        /// then this raises an <see cref="IndexError"/>.
        /// </summary>
        /// <remarks>
        /// Should call <see cref="__GetItem__(Slice)"/> underneath.
        /// </remarks>
        T this[int index] { get; }

        /// <summary>
        /// Returns the sub-sequence at the given start and end (exclusive) indices
        /// in the sequence. Supports negative indices to count from the back.
        /// If either index is out of range, then this raises an
        /// <see cref="IndexError"/>.
        /// </summary>
        /// <remarks>
        /// Should call <see cref="__GetItem__(Slice)"/> underneath.
        /// </remarks>
        ISequence<T> this[int start, int end] { get; }

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
        ISequence<T> this[int start, int end, int step] { get; }

        /// <summary>
        /// Returns the number of times the given element appears in the
        /// sequence as evaluated by Sharpy's equality resolution.
        /// </summary>
        uint Count(T x);

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
    public interface ISequence<S, T> : ISequence<T>
    {
        /// <summary>
        /// Returns the sub-sequence at the given start and end (exclusive) indices
        /// in the sequence. Supports negative indices to count from the back.
        /// If either index is out of range, then this raises an
        /// <see cref="IndexError"/>.
        /// </summary>
        /// <remarks>
        /// Should call <see cref="__GetItem__(Slice)"/> underneath.
        /// </remarks>
        new S this[int start, int end] { get; }

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
        new S this[int start, int end, int step] { get; }

        /// <summary>
        /// Returns the sub-sequence at the given start and end (exclusive)
        /// indices in the sequence, skipping every step elements after the
        /// start. Supports negative indices to count from the back. If either
        /// index is out of range, then this raises an
        /// <see cref="IndexError"/>. If the step is less than 1, then an
        /// empty sequence is returned.
        /// </summary>
        new S __GetItem__(Slice slice);
    }
}
