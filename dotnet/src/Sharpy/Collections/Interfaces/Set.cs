namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only sets.
    /// </summary>
    public interface Set<T> : Collection<T>, Equatable<Set<T>>, Inequatable<Set<T>>, Comparable<Set<T>>
    {
        Set<T> __And__(Set<T> other);

        Set<T> __Or__(Set<T> other);

        Set<T> __Sub__(Set<T> other);

        Set<T> __RSub__(Set<T> other);

        Set<T> __XOr__(Set<T> other);

        Set<T> __ROr__(Set<T> other);

        bool IsDisjoint(Set<T> other);
    }

    public interface Set<S, T> : Set<T>
    {
        S __And__(S other);

        S __Or__(S other);

        S __Sub__(S other);

        S __RSub__(S other);

        S __XOr__(S other);

        S __ROr__(S other);

        bool IsDisjoint(S other);
    }
}
