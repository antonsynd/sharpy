namespace Sharpy
{
    public sealed partial class List<T>
    {
        public Iterator<T> __Reversed__()
        {
            return new ListReverseIterator<T>(this);
        }
    }
}
