namespace Sharpy
{
    public interface ISequence<T> : IIterable<T>
    {
        bool Contains(T x);

        uint Len();
    }
}
