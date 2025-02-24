namespace Sharpy
{
    public interface MutableSet<T> : Set<T>
    {
        void Add(T x);

        void Discard(T x);

        void Clear();

        T Pop();

        void Remove(T x);

        void __IOr__(Set<T> other);

        void __IAnd__(Set<T> other);

        void __IXOr__(Set<T> other);

        void __ISub__(Set<T> other);
    }
}
