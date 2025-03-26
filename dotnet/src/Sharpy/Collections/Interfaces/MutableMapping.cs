namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for mutable mappings.
    /// </summary>
    public interface MutableMapping<K, V> : Mapping<K, V> where K : notnull
    {
        void __SetItem__(K key, V value);

        void __DelItem__(K key);

        void __IOr__(Mapping<K, V> other);

        void __IOr__(Iterable<(K, V)> other);

        V Pop(K key);

        V Pop(K key, V @default);

        (K, V) PopItem(bool last = false);

        void Clear();

        void Update(Mapping<K, V> other);

        void Update(Iterable<(K, V)> other);

        V SetDefault(K key, V @default);
    }
}
