namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for mapping views over pairs of keys and values.
/// </summary>
public interface IItemsView<K, V> : IMappingView<(K, V)> where K : notnull
{
}
