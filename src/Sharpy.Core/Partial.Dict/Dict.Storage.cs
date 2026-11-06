using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Sharpy
{
    /// <remarks>
    /// Storage is CPython's compact layout: an append-only <see cref="_entries"/> array holds the
    /// entries in insertion order, and a hash index maps each key to its slot. Removing a key leaves
    /// a tombstone, so nothing moves and no slot is reused until the array is compacted (#2440).
    /// <see cref="Dictionary{TKey, TValue}"/> serves as the hash index rather than a hand-rolled table
    /// because it keeps the non-randomized string hashing and the key-collision defence, and because
    /// the element accessors stay inlinable-shaped around its <c>TryGetValue</c> (#2404).
    /// <see cref="System.Collections.Generic.OrderedDictionary{TKey, TValue}"/> does not fit: its
    /// <c>Remove</c> shifts entries, O(n), and it is net9.0+ only.
    /// <para>
    /// The None key has no index entry (the index refuses a null key). It occupies an ordinary slot,
    /// recorded in <see cref="_nullSlot"/>, so it takes its position from the same array.
    /// </para>
    /// </remarks>
    public sealed partial class Dict<K, V>
    {
        private struct Entry
        {
            public K Key;
            public V Value;
            public bool Live;
        }

        private const int MinimumCapacity = 4;

        /// <summary>Key to slot, for every live entry except the None key.</summary>
        private readonly Dictionary<K, int> _index;

        private Entry[] _entries;

        /// <summary>
        /// Slots handed out so far, live or tombstone. Invariant: the slot below it is live, or it is 0,
        /// so the last entry is found in O(1).
        /// </summary>
        private int _used;

        /// <summary>
        /// Invariant: every slot below it is a tombstone, and it is the first live slot when any exists,
        /// so the first entry is found in O(1).
        /// </summary>
        private int _first;

        /// <summary>
        /// Live entries. Counted here rather than read from the index: the index holds no None key, and
        /// a key whose hash changed after insertion can leave a stale index node behind at compaction.
        /// </summary>
        private int _count;

        /// <summary>The None key's slot, or -1.</summary>
        private int _nullSlot = -1;

        /// <summary>Bumped when a new key arrives. Enumerators refuse to continue past a change.</summary>
        private int _version;

        private static bool IsNullKey(K key) => key is null;

        /// <summary>The key's slot, or -1. Shared by every lookup, so no route can disagree about a key.</summary>
        private int FindSlot(K key)
        {
            if (IsNullKey(key))
            {
                return _nullSlot;
            }

            return _index.TryGetValue(key, out int slot) ? slot : -1;
        }

        /// <summary>
        /// Inserts a key <see cref="FindSlot"/> just reported absent. Out of line because it is the cold
        /// half of the indexer's setter, doing the work <c>Dictionary.set_Item</c>'s insert path did.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InsertNew(K key, V value)
        {
            int slot = Append(key, value);
            if (IsNullKey(key))
            {
                _nullSlot = slot;
            }
            else
            {
                _index.Add(key, slot);
            }
        }

        /// <summary>
        /// Stores under the None key, for the <c>object</c>-keyed routes, where a null key can reach a
        /// dict whose key type is a non-nullable value type.
        /// </summary>
        private void StoreNull(V value)
        {
            if (_nullSlot >= 0)
            {
                _entries[_nullSlot].Value = value;
            }
            else
            {
                _nullSlot = Append(default!, value);
            }
        }

        private int Append(K key, V value)
        {
            if (_used == _entries.Length)
            {
                MakeRoom();
            }

            int slot = _used;
            ref Entry entry = ref _entries[slot];
            entry.Key = key;
            entry.Value = value;
            entry.Live = true;
            _used = slot + 1;
            _count++;
            _version++;
            return slot;
        }

        /// <summary>
        /// Called with the array full. Squeezes the tombstones out, into a doubled array unless that
        /// alone frees at least half of it, so a remove-and-insert workload stays bounded and each
        /// call's O(live) cost is paid for by the appends it makes room for. Runs only on the insert
        /// path, which invalidates enumerators anyway, so no live enumerator sees a slot move.
        /// </summary>
        private void MakeRoom()
        {
            int capacity = _entries.Length;
            int newCapacity = capacity == 0 || _count > capacity / 2
                ? Math.Max(MinimumCapacity, capacity * 2)
                : capacity;
            Entry[] source = _entries;
            Entry[] target = newCapacity == capacity ? source : new Entry[newCapacity];

            if (_count == _used)
            {
                Array.Copy(source, target, _used);
            }
            else
            {
                int to = 0;
                for (int from = _first; from < _used; from++)
                {
                    if (!source[from].Live)
                    {
                        continue;
                    }

                    target[to] = source[from];
                    if (from == _nullSlot)
                    {
                        _nullSlot = to;
                    }
                    else
                    {
                        _index[source[from].Key] = to;
                    }

                    to++;
                }

                if (target == source)
                {
                    Array.Clear(target, to, _used - to);
                }
            }

            _entries = target;
            _used = _count;
            _first = 0;
        }

        /// <summary>Removes the entry in a live slot and returns its value, restoring both end invariants.</summary>
        private V RemoveSlot(int slot)
        {
            ref Entry entry = ref _entries[slot];
            V value = entry.Value;
            if (slot == _nullSlot)
            {
                _nullSlot = -1;
            }
            else
            {
                _index.Remove(entry.Key);
            }

            entry = default;
            _count--;

            // Each loop steps over a tombstone at most once in its lifetime, so both are amortized O(1).
            while (_used > 0 && !_entries[_used - 1].Live)
            {
                _used--;
            }

            while (_first < _used && !_entries[_first].Live)
            {
                _first++;
            }

            if (_used == 0)
            {
                _first = 0;
            }

            return value;
        }

        private EntryCursor CreateCursor() => new EntryCursor(this);

        internal IEnumerable<(K, V)> EnumerateItems()
        {
            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                yield return (cursor.Key, cursor.Value);
            }
        }

        internal IEnumerable<V> EnumerateValuesInOrder()
        {
            var cursor = CreateCursor();
            while (cursor.MoveNext())
            {
                yield return cursor.Value;
            }
        }

        /// <summary>
        /// Walks the live entries in insertion order. A new key invalidates it, as it does a
        /// <see cref="Dictionary{TKey, TValue}"/> enumerator; a removal, a re-store or a clear does not,
        /// also as there: tombstones are skipped, and <see cref="_used"/> is re-read at each step.
        /// </summary>
        private struct EntryCursor
        {
            private readonly Dict<K, V> _dict;
            private readonly int _version;
            private int _next;

            public K Key;
            public V Value;
            public int Slot;

            public EntryCursor(Dict<K, V> dict)
            {
                _dict = dict;
                _version = dict._version;
                _next = dict._first;
                Key = default!;
                Value = default!;
                Slot = -1;
            }

            public bool MoveNext()
            {
                var dict = _dict;
                if (_version != dict._version)
                {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                var entries = dict._entries;
                while (_next < dict._used)
                {
                    int slot = _next++;
                    ref Entry entry = ref entries[slot];
                    if (entry.Live)
                    {
                        Key = entry.Key;
                        Value = entry.Value;
                        Slot = slot;
                        return true;
                    }
                }

                Key = default!;
                Value = default!;
                Slot = -1;
                return false;
            }

            public void Reset()
            {
                if (_version != _dict._version)
                {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                _next = _dict._first;
                Key = default!;
                Value = default!;
                Slot = -1;
            }
        }
    }
}
