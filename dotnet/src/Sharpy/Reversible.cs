namespace Sharpy
{
    public interface Reversible<T> : Iterable<T>
    {
        Iterator<T> __Reversed__();
    }
}
