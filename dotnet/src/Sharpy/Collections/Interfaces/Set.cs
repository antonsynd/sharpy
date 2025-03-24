namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only sets.
    /// </summary>
    public interface Set<T> : Collection<T>, Equatable<Set<T>>, Comparable<Set<T>>
    {
        bool __Ne__(Set<T> other);

        Set<T> __And__(Set<T> other);

        Set<T> __Or__(Set<T> other);

        Set<T> __Sub__(Set<T> other);

        Set<T> __RSub__(Set<T> other);

        Set<T> __XOr__(Set<T> other);

        Set<T> __ROr__(Set<T> other);

        bool IsDisjoint(Set<T> other);
    }
}
