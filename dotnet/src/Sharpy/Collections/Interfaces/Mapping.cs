namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only mappings.
    /// </summary>
    public interface Mapping<K, V> : Collection<K> where K : notnull
    {
        V __GetItem__(K k);


        Mapping<K, V> __Or__(Mapping<K, V> other);

        Mapping<K, V> __Or__(Iterable<(K, V)> other);

        KeysView<K> Keys();

        ItemsView<K, V> Items();

        ValuesView<V> Values();

        V? Get(K k);

        V Get(K k, V @default);
    }
}
