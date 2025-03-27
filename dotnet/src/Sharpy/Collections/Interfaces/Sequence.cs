namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only sequences.
    /// </summary>
    public interface Sequence<S, T> : Collection<T>, Reversible<T>
    {
        /// <summary>
        /// Returns the item at the given index in the sequence. Supports negative
        /// indices to get or set from the back. If the index is out of range,
        /// then this raises an <see cref="IndexError"/>.
        /// </summary>
        /// <remarks>
        /// Should call <see cref="__GetItem__()"/> underneath.
        /// </remarks>
        T this[int index] { get; }

        /// <summary>
        /// Returns the sub-sequence at the given start and end (exclusive) indices
        /// in the sequence. Supports negative indices to count from the back.
        /// If either index is out of range, then this raises an
        /// <see cref="IndexError"/>.
        /// </summary>
        /// <remarks>
        /// Should call <see cref="__GetItem__()"/> underneath.
        /// </remarks>
        S this[int start, int end] { get; }

        /// <summary>
        /// Returns the sub-sequence at the given start and end (exclusive)
        /// indices in the sequence, skipping every step elements after the
        /// start. Supports negative indices to count from the back. If either
        /// index is out of range, then this raises an
        /// <see cref="IndexError"/>. If the step is less than 1, then an
        /// empty sequence is returned.
        /// </summary>
        /// <remarks>
        /// Should call <see cref="__GetItem__()"/> underneath.
        /// </remarks>
        S this[int start, int end, int step] { get; }

        uint Count(T x);

        uint Index(T x, int start = 0, int end = -1);

        T __GetItem__(int index);

        S __GetItem__(Slice slice);
    }
}
