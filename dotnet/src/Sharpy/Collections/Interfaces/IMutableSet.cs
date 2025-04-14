namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for mutable sets.
/// </summary>
public interface IMutableSet<T> : ISet<T>
{
    void Add(T x);

    void Discard(T x);

    void Clear();

    T Pop();

    void Remove(T x);

    void __IOr__(ISet<T> other);

    void __IAnd__(ISet<T> other);

    void __IXOr__(ISet<T> other);

    void __ISub__(ISet<T> other);
}

public interface IMutableSet<S, T> : IMutableSet<T>, ISet<S, T>
    where S : ISet<S, T>, ILessThanOrEquatable<S>, IGreaterThanOrEquatable<S>
{
    /// <inheritdoc/>
    void __IOr__(S other);

    /// <inheritdoc/>
    void __IAnd__(S other);

    /// <inheritdoc/>
    void __IXOr__(S other);

    /// <inheritdoc/>
    void __ISub__(S other);
}
