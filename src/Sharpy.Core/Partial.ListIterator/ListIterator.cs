namespace Sharpy.Core
{
    public sealed partial class ListIterator<T> : Iterator<T>
    {
        private readonly List<T> _list;
        private uint _index = 0;

        internal ListIterator(List<T> list)
        {
            _list = list;
        }
    }
}
