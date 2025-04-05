namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for mutable sequences.
    /// </summary>
    public interface MutableSequence<T> : Sequence<T>, InplaceAddable<Sequence<T>>
    {
        /// <summary>
        /// Returns or sets the element at the given index. Supports negative
        /// indices to get or set from the back. If the index is out of range,
        /// then this raises an <see cref="IndexError"/>.
        /// </summary>
        new T this[int index] { get; set; }

        /// <summary>
        /// Returns the element at the given index. Supports negative indices
        /// to get or set from the back. If the index is out of range, then
        /// this raises an <see cref="IndexError"/>.
        /// </summary>
        new MutableSequence<T> this[int start, int end] { get; set; }

        /// <see cref="this[int, int]"/>
        new MutableSequence<T> this[int start, int end, int step] { get; set; }

        void Append(T x);

        /// <remarks>
        /// Iterable&lt;T&gt; extends IEnumerable&lt;T&gt;.
        /// </remarks>
        void Extend(IEnumerable<T> enumerable);

        void Clear();

        void Insert(int i, T x);

        T Pop(int i = -1);

        void Remove(T x);

        void Reverse();

        void __DelItem__(int i);

        void __DelItem__(Slice slice);

        void __SetItem__(int i, T x);

        void __SetItem__(Slice slice, Sequence<T> other);
    }

    public interface MutableSequence<S, T> : MutableSequence<T>, Sequence<S, T>
    {
        /// <summary>
        /// Returns or sets the element at the given index. Supports negative
        /// indices to get or set from the back. If the index is out of range,
        /// then this raises an <see cref="IndexError"/>.
        /// </summary>
        new T this[int index] { get; set; }

        /// <summary>
        /// Returns the element at the given index. Supports negative indices
        /// to get or set from the back. If the index is out of range, then
        /// this raises an <see cref="IndexError"/>.
        /// </summary>
        new S this[int start, int end] { get; set; }

        /// <inheritdoc/>
        new S this[int start, int end, int step] { get; set; }

        /// <inheritdoc/>
        void __SetItem__(Slice slice, S other);
    }
}
