using Sharpy.Core;

namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for mutable mappings.
/// </summary>
public interface IMutableMapping<K, V> : IMapping<K, V> where K : notnull
{
    /// <summary>
    /// Gets the value of or sets the value of the given key.
    /// </summary>
    new V this[K key] { get; set; }

    /// <summary>
    /// Sets the given key to the given value.
    /// </summary>
    void __SetItem__(K key, V value);

    /// <summary>
    /// Deletes the value at the given key. Raises <see cref="KeyError"/>
    /// if the key does not exist in this mapping.
    /// </summary>
    void __DelItem__(K key);

    /// <summary>
    /// Updates this mapping with the pairs from the other mapping, with
    /// conflicting keys using the value from the other mapping.
    /// </summary>
    void __IOr__(IMapping<K, V> other);

    /// <summary>
    /// Updates this mapping with the key-value tuples in the iterable,
    /// with conflicting keys using the value from the iterable.
    /// </summary>
    void __IOr__(IIterable<(K, V)> iterable);

    /// <summary>
    /// Removes the value at the given key. If the key does not exist,
    /// this raises a <see cref="KeyError"/>.
    /// </summary>
    V Pop(K key);

    /// <summary>
    /// Removes the value at the given key. If the key does not exist,
    /// this returns the given default value.
    /// </summary>
    V Pop(K key, V @default);

    /// <summary>
    /// Removes the first key-value pair from the mapping, by insertion
    /// order. If last is True, then returns the last such pair.
    /// </summary>
    (K, V) PopItem(bool last = false);

    /// <summary>
    /// Updates this mapping with the pairs from the other mapping, with
    /// conflicting keys using the value from the other mapping.
    /// </summary>
    /// <remarks>
    /// Internally should call <see cref="__IOr__(IMapping&lt;K, V&gt;)"/> if the class is
    /// not sealed.
    /// </remarks>
    void Update(IMapping<K, V> other);

    /// <summary>
    /// Updates this mapping with the key-value tuples in the iterable,
    /// with conflicting keys using the value from the iterable.
    /// </summary>
    /// <remarks>
    /// Internally should call
    /// <see cref="__IOr__(IIterable&lt;System.ValueTuple{K, V}&gt;)"/> if the class is
    /// not sealed.
    /// </remarks>
    void Update(IIterable<(K, V)> other);

    /// <summary>
    /// Returns the value for the given key, setting it to the given default
    /// if the key is not in the mapping, before returning it.
    /// </summary>
    V SetDefault(K key, V @default);

    // static abstract IMapping<K, V> operator |(IMapping<K, V> left, IMapping<K, V> right);

    // static abstract IMapping<K, V> operator |(IMapping<K, V> left, IIterable<(K, V)> right);
}

/// <summary>
/// Interface for mutable mappings that can be updated with other
/// mutable mappings via a curiously recursive template.
/// </summary>
public interface IMutableMapping<M, K, V> : IMutableMapping<K, V>, IMapping<M, K, V> where K : notnull
{
    /// <inheritdoc/>
    void __IOr__(M other);

    /// <inheritdoc/>
    void Update(M other);

    // static abstract M operator |(M left, M right);
}
