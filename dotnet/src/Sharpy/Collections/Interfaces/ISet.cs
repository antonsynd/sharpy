namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for read-only sets.
/// </summary>
public interface ISet<T> : ICollection<T>, IEquatable<Set<T>>, IInequatable<Set<T>>
{
    ISet<T> __And__(ISet<T> other);

    ISet<T> __Or__(ISet<T> other);

    ISet<T> __Sub__(ISet<T> other);

    ISet<T> __RSub__(ISet<T> other);

    ISet<T> __XOr__(ISet<T> other);

    ISet<T> __ROr__(ISet<T> other);

    bool IsDisjoint(ISet<T> other);

    // static abstract ISet<T> operator &(ISet<T> left, ISet<T> right);

    // static abstract ISet<T> operator |(ISet<T> left, ISet<T> right);

    // static abstract ISet<T> operator -(ISet<T> left, ISet<T> right);

    // static abstract ISet<T> operator ^(ISet<T> left, ISet<T> right);
}

public interface ISet<S, T> : ISet<T>
{
    S __And__(S other);

    S __Or__(S other);

    S __Sub__(S other);

    S __RSub__(S other);

    S __XOr__(S other);

    S __ROr__(S other);

    bool IsDisjoint(S other);

    // static abstract S operator &(S left, S right);

    // static abstract S operator |(S left, S right);

    // static abstract S operator -(S left, S right);

    // static abstract S operator ^(S left, S right);
}
