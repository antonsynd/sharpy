namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only sets.
    /// </summary>
    public interface Set<T> : Collection<T>
    {
        bool __Le__(Set<T> other);

        bool __Lt__(Set<T> other);

        bool __Eq__(Set<T> other);

        bool __Ne__(Set<T> other);

        bool __Gt__(Set<T> other);

        bool __Ge__(Set<T> other);

        Set<T> __And__(Set<T> other);

        Set<T> __Or__(Set<T> other);

        Set<T> __Sub__(Set<T> other);

        Set<T> __RSub__(Set<T> other);

        Set<T> __XOr__(Set<T> other);

        Set<T> __ROr__(Set<T> other);

        bool IsDisjoint(Set<T> other);
    }
}
