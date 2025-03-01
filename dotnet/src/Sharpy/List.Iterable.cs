namespace Sharpy
{
    public sealed partial class List<T>
    {
        public Iterator<T> __Iter__()
        {
            return new ListIterator<T>(this);
        }
    }
}
