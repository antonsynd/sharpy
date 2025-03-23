namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only mappings.
    /// </summary>
    public interface Mapping<K, V> : Collection<(K, V)> where K : notnull where V : notnull
    {
        V __GetItem__(K k);

        bool __Contains__(K k);
        bool Contains(K k);

        KeysView<K> Keys();
        ItemsView<K, V> Items();
        ValuesView<V> Values();

        V Get(K k);
    }
}
