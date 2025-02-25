namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for mutable sequences.
    /// </summary>
    public interface MutableSequence<T> : Sequence<T>
    {
        void __SetItem__(T x);

        void __DelItem__(T x);

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
    }
}
