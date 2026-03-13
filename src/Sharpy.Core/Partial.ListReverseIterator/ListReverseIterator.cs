namespace Sharpy
{
    /// <summary>Reverse iterator for List.</summary>
    public sealed partial class ListReverseIterator<T> : Iterator<T>
    {
        private readonly List<T> _list;
        private uint _index = 0;

        internal ListReverseIterator(List<T> list)
        {
            _list = list;
        }
    }
}
