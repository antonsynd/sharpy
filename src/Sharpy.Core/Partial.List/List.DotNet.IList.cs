using System.Collections.Generic;
namespace Sharpy.Core
{
    public sealed partial class List<T>
    {
        // ICollection<T>.Count - delegate to __Len__
        int System.Collections.Generic.ICollection<T>.Count => __Len__();

        // IReadOnlyCollection<T>.Count - delegate to __Len__
        int System.Collections.Generic.IReadOnlyCollection<T>.Count => __Len__();

        // IList<T>.IndexOf - delegate to Index(), returning -1 if not found
        int IList<T>.IndexOf(T item)
        {
            try
            {
                return (int)Index(item);
            }
            catch (ValueError)
            {
                return -1;
            }
        }

        // IList<T>.RemoveAt - delegate to __DelItem__
        void IList<T>.RemoveAt(int index) => __DelItem__(index);

        // IList<T>.Insert - already implemented in List.IMutableSequence.cs

        // IList<T>.this[int] - explicit implementation to avoid conflicts
        T IList<T>.this[int index]
        {
            get => __GetItem__(index);
            set => __SetItem__(index, value);
        }

        // IReadOnlyList<T>.this[int] - explicit implementation
        T IReadOnlyList<T>.this[int index] => __GetItem__(index);
    }
}
