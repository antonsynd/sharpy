namespace Sharpy
{
    public interface Sequence<T> : Collection<T>, Reversible<T>
    {
        T __GetItem__();

        uint Count(T x);

        uint Index(T x, int start = 0, int end = -1);
    }
}
