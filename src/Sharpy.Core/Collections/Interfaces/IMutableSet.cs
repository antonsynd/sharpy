using Sharpy.Core;

namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for mutable sets.
/// </summary>
public interface IMutableSet<T> : ISet<T>
{
    void Discard(T x);

    T Pop();

    new void Remove(T x);

    void __IOr__(ISet<T> other);

    void __IAnd__(ISet<T> other);

    void __IXOr__(ISet<T> other);

    void __ISub__(ISet<T> other);
}

public interface IMutableSet<TSet, TElement>
    : IMutableSet<TElement>,
      ISet<TSet, TElement>
      where TSet
        : ISet<TSet, TElement>,
          ILessThanOrEquatable<TSet>,
          IGreaterThanOrEquatable<TSet>
{
    /// <inheritdoc/>
    void __IOr__(TSet other);

    /// <inheritdoc/>
    void __IAnd__(TSet other);

    /// <inheritdoc/>
    void __IXOr__(TSet other);

    /// <inheritdoc/>
    void __ISub__(TSet other);
}
