namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only sequences.
    /// </summary>
    public interface Sequence<S, T> : Collection<T>, Reversible<T> where S : notnull where T : notnull
    {
        uint Count(T x);

        uint Index(T x, int start = 0, int end = -1);

        S __GetItem__();

        T __GetItem__(int index);

        S __GetItem__(Slice slice);
    }
}
