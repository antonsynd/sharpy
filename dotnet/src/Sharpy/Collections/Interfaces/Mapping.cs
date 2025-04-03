namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only mappings.
    /// </summary>
    public interface Mapping<K, V> : Collection<K> where K : notnull
    {
        /// <summary>
        /// Returns the value at the given key in the mapping. If the key does
        /// not exist then this raises a <see cref="KeyError"/>.
        /// </summary>
        /// <remarks>
        /// Should call <see cref="__GetItem__()"/> underneath.
        /// </remarks>
        V this[K key] { get; }

        /// <summary>
        /// Gets the value for the given key. Raises <see cref="KeyError"/>
        /// if the key doesn't exist in the mapping.
        /// </summary>
        V __GetItem__(K key);

        /// <summary>
        /// Returns the union of this mapping and the other mapping, with
        /// conflicting keys using the value from the other mapping.
        /// </summary>
        Mapping<K, V> __Or__(Mapping<K, V> other);

        /// <summary>
        /// Returns the union of this mapping and the iterable of key-value
        /// tuples, with conflicting keys using the value from the other
        /// mapping.
        /// </summary>
        Mapping<K, V> __Or__(Iterable<(K, V)> iterable);

        /// <summary>
        /// Returns a view over the keys of this mapping, in insertion order.
        /// </summary>
        KeysView<K> Keys();

        /// <summary>
        /// Returns a view over the items (key-value pairs) of this mapping,
        /// in insertion order.
        /// </summary>
        ItemsView<K, V> Items();

        /// <summary>
        /// Returns a view over the values of this mapping, in insertion order.
        /// </summary>
        ValuesView<V> Values();

        /// <summary>
        /// Gets the value with the given key, if it exists. If it doesn't
        /// exist, it returns <see cref="null"/>.
        /// </summary>
        /// <remarks>
        /// Internally, this should call <see cref="__GetItem__()"/>, unless
        /// the class is sealed, in which case it doesn't matter.
        /// </remarks>
        V? Get(K key);

        /// <summary>
        /// Gets the value with the given key, if it exists. If it doesn't
        /// exist, it returns the given default value.
        /// </summary>
        /// <remarks>
        /// Internally, this should call <see cref="__GetItem__()"/>, unless
        /// the class is sealed, in which case it doesn't matter.
        /// </remarks>
        V Get(K key, V @default);
    }

    public interface Mapping<M, K, V> : Mapping<K, V> where K : notnull
    {
        M __Or__(M other);
    }
}
