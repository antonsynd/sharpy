using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    using static Builtins;

    public sealed partial class Dict<K, V> : IDict
    {
        /// <summary>
        /// The slot of an <c>object</c> key, or -1. A null key is the None slot whatever K is; a key that
        /// is not a K is simply absent.
        /// </summary>
        private int FindSlotOfObject(object key)
        {
            if (key is null)
            {
                return _nullSlot;
            }

            return key is K typedKey ? FindSlot(typedKey) : -1;
        }

        object? IDict.this[object key]
        {
            get
            {
                int slot = FindSlotOfObject(key);
                if (slot < 0)
                {
                    throw new KeyError(key);
                }

                return _entries[slot].Value;
            }
            set
            {
                StoreObject(key, value);
            }
        }

        private void StoreObject(object? key, object? value)
        {
            if (key is null)
            {
                StoreNull((V)value!);
            }
            else
            {
                this[(K)key] = (V)value!;
            }
        }

        IEnumerable<(object?, object?)> IDict.Items()
        {
            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                yield return (cursor.Key, cursor.Value);
            }
        }

        IEnumerable<object?> IDict.Keys()
        {
            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                yield return cursor.Key;
            }
        }

        IEnumerable<object?> IDict.Values()
        {
            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                yield return cursor.Value;
            }
        }

        bool IDict.Contains(object key) => FindSlotOfObject(key) >= 0;

        Optional<object?> IDict.Get(object key)
        {
            int slot = FindSlotOfObject(key);
            return slot >= 0 ? Optional<object?>.Some((object?)_entries[slot].Value) : Optional<object?>.None;
        }

        object? IDict.Get(object key, object? defaultValue)
        {
            int slot = FindSlotOfObject(key);
            return slot >= 0 ? _entries[slot].Value : defaultValue;
        }

        object? IDict.Pop(object key)
        {
            int slot = FindSlotOfObject(key);
            if (slot < 0)
            {
                throw new KeyError(key);
            }

            return RemoveSlot(slot);
        }

        object? IDict.Pop(object key, object? defaultValue)
        {
            int slot = FindSlotOfObject(key);
            return slot >= 0 ? RemoveSlot(slot) : defaultValue;
        }

        (object?, object?) IDict.PopItem(bool last)
        {
            var (k, v) = PopItem(last);
            return (k, v);
        }

        object? IDict.SetDefault(object key, object? defaultValue)
        {
            int slot = key is null ? _nullSlot : FindSlot((K)key);
            if (slot >= 0)
            {
                return _entries[slot].Value;
            }

            var typedValue = (V)defaultValue!;
            StoreObject(key, typedValue);
            return typedValue;
        }

        void IDict.Clear()
        {
            Clear();
        }

        IDict IDict.Copy()
        {
            return Copy();
        }

        void IDict.Update(IDict other)
        {
            foreach (var (k, v) in other.Items())
            {
                StoreObject(k, v);
            }
        }

        void IDict.Update(IEnumerable<(object?, object?)> other)
        {
            foreach (var (k, v) in other)
            {
                StoreObject(k, v);
            }
        }

        void IDict.Remove(object key)
        {
            int slot = FindSlotOfObject(key);
            if (slot < 0)
            {
                throw new KeyError(key);
            }

            RemoveSlot(slot);
        }
    }
}
