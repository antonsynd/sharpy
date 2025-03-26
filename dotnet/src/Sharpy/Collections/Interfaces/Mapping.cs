namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only mappings.
    /// </summary>
    public interface Mapping<K, V> : Collection<K> where K : notnull
    {
        V __GetItem__(K k);

        KeysView<K> Keys();

        ItemsView<K, V> Items();

        ValuesView<V> Values();

        V? Get(K k);

        V Get(K k, V @default);
    }
}
