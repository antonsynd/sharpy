namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for mutable mappings.
    /// </summary>
    public interface MutableMapping<K, V> : Mapping<K, V> where K : notnull
    {
        void __SetItem__(K k, V v);

        void __DelItem__(K k);

        V Pop(K k);

        V Pop(K k, V @default);

        (K, V) PopItem();

        void Clear();

        void Update(Mapping<K, V> other);

        void Update(Iterable<(K, V)> other);

        V SetDefault(K k, V @default);
    }
}
