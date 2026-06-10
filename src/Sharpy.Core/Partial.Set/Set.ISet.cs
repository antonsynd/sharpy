using System.Collections;

namespace Sharpy
{
    using static Builtins;

    public sealed partial class Set<T> : ISet
    {
        void ISet.Add(object? item)
        {
            _set.Add((T)item!);
        }

        void ISet.Discard(object? item)
        {
            if (item is T typedItem)
            {
                _set.Remove(typedItem);
            }
        }

        void ISet.Remove(object item)
        {
            if (item is T typedItem)
            {
                if (!_set.Remove(typedItem))
                {
                    throw new KeyError(Repr(item));
                }

                return;
            }

            throw new KeyError(Repr(item));
        }

        object? ISet.Pop()
        {
            return Pop();
        }

        void ISet.Clear()
        {
            Clear();
        }

        ISet ISet.Copy()
        {
            return Copy();
        }

        bool ISet.Contains(object item)
        {
            return item is T typedItem && _set.Contains(typedItem);
        }

        void ISet.Update(IEnumerable items)
        {
            foreach (var item in items)
            {
                _set.Add((T)item!);
            }
        }
    }
}
