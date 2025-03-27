namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for mapping views over pairs of keys and values.
    /// </summary>
    public interface ItemsView<K, V> : MappingView<(K, V)> where K : notnull
    {
    }
}
