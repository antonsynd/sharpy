namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for mapping views over pairs of keys and values.
    /// </summary>
    public interface ItemsView<K, V> : Set<(K, V)>, MappingView where K : notnull where V : notnull
    {
    }
}
