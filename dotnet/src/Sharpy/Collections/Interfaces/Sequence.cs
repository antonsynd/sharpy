namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for read-only sequences.
    /// </summary>
    public interface Sequence<T> : Collection<T>, Reversible<T>
    {
        uint Count(T x);

        uint Index(T x, int start = 0, int end = -1);

        T __GetItem__(int index);
    }
}
