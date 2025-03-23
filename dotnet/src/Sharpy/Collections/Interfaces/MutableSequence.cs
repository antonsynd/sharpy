namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for mutable sequences.
    /// </summary>
    public interface MutableSequence<S, T> : Sequence<S, T> where S : notnull where T : notnull
    {
        void Append(T x);

        /// <remarks>
        /// Iterable<T> extends IEnumerable<T>.
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

        void __SetItem__(S other);

        void __SetItem__(Slice slice, S other);
    }
}
