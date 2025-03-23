namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for mutable mappings.
    /// </summary>
    public interface MutableMapping<K, V> : Mapping<K, V> where K : notnull where V : notnull
    {
    }
}
