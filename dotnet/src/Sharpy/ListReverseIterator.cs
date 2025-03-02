
namespace Sharpy
{
    public sealed partial class ListReverseIterator<T> : Iterator<T> where T : IComparable<T>, IEquatable<T>
    {
        private readonly List<T> _list;
        private uint _index = 0;

        internal ListReverseIterator(List<T> list)
        {
            _list = list;
        }
    }
}
