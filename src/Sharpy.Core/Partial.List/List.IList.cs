using System.Collections;

namespace Sharpy
{
    using static Builtins;

    public sealed partial class List<T> : IList
    {
        object? IList.this[object index]
        {
            get
            {
                return this[(int)index];
            }
            set
            {
                this[(int)index] = (T)value!;
            }
        }

        void IList.Append(object? item)
        {
            Append((T)item!);
        }

        void IList.Extend(IEnumerable items)
        {
            foreach (var item in items)
            {
                _list.Add((T)item!);
            }
        }

        void IList.Insert(object index, object? item)
        {
            Insert((int)index, (T)item!);
        }

        object? IList.Pop(int index)
        {
            return Pop(index);
        }

        void IList.Remove(object item)
        {
            Remove((T)item);
        }

        void IList.Clear()
        {
            Clear();
        }

        IList IList.Copy()
        {
            return Copy();
        }

        void IList.Reverse()
        {
            Reverse();
        }

        void IList.Sort()
        {
            Sort();
        }

        int IList.Index(object item)
        {
            return Index((T)item);
        }

        int IList.CountOf(object item)
        {
            if (item is T typedItem)
            {
                return Count(typedItem);
            }

            return 0;
        }

        bool IList.Contains(object item)
        {
            if (item is T typedItem)
            {
                return Contains(typedItem);
            }

            return false;
        }
    }
}
