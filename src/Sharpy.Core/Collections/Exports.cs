using Sharpy.Core;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Sharpy.Collections
{
    /// <summary>
    /// A deque (double-ended queue) is a generalization of stacks and queues
    /// that supports adding and removing elements from either end.
    /// </summary>
    public class Deque<T> : IReadOnlyCollection<T>
    {
        private readonly System.Collections.Generic.LinkedList<T> _list;

        public Deque()
        {
            _list = new System.Collections.Generic.LinkedList<T>();
        }

        public Deque(IEnumerable<T> iterable)
        {
            _list = new System.Collections.Generic.LinkedList<T>(iterable);
        }

        /// <summary>
        /// Add x to the right side of the deque.
        /// </summary>
        public void Append(T x)
        {
            _list.AddLast(x);
        }

        /// <summary>
        /// Add x to the left side of the deque.
        /// </summary>
        public void Appendleft(T x)
        {
            _list.AddFirst(x);
        }

        /// <summary>
        /// Remove and return an element from the right side of the deque.
        /// If no elements are present, raises an IndexError.
        /// </summary>
        public T Pop()
        {
            if (_list.Count == 0)
            {
                throw new IndexError("pop from an empty deque");
            }

            T value = _list.Last!.Value;
            _list.RemoveLast();
            return value;
        }

        /// <summary>
        /// Remove and return an element from the left side of the deque.
        /// If no elements are present, raises an IndexError.
        /// </summary>
        public T Popleft()
        {
            if (_list.Count == 0)
            {
                throw new IndexError("pop from an empty deque");
            }

            T value = _list.First!.Value;
            _list.RemoveFirst();
            return value;
        }

        /// <summary>
        /// Remove all elements from the deque.
        /// </summary>
        public void Clear()
        {
            _list.Clear();
        }

        /// <summary>
        /// Extend the right side of the deque by appending elements from the iterable.
        /// </summary>
        public void Extend(IEnumerable<T> iterable)
        {
            foreach (var item in iterable)
            {
                _list.AddLast(item);
            }
        }

        /// <summary>
        /// Extend the left side of the deque by appending elements from the iterable.
        /// </summary>
        public void Extendleft(IEnumerable<T> iterable)
        {
            foreach (var item in iterable)
            {
                _list.AddFirst(item);
            }
        }

        /// <summary>
        /// Gets the number of elements in the deque.
        /// </summary>
        public int Count => _list.Count;

        /// <summary>
        /// Deprecated: Use <see cref="Count"/> instead.
        /// </summary>
        public int __Len__() => Count;

        /// <summary>
        /// Deprecated: Use <see cref="GetEnumerator()"/> instead.
        /// </summary>
        public Iterator<T> __Iter__()
        {
            return new EnumeratorIterator<T>(_list.GetEnumerator());
        }

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _list.GetEnumerator();
    }

    /// <summary>
    /// A Counter is a dict subclass for counting hashable objects.
    /// </summary>
    public class Counter<T> where T : notnull
    {
        private readonly System.Collections.Generic.Dictionary<T, int> _counts;

        public Counter()
        {
            _counts = new System.Collections.Generic.Dictionary<T, int>();
        }

        public Counter(IEnumerable<T> iterable)
        {
            _counts = new System.Collections.Generic.Dictionary<T, int>();
            foreach (var item in iterable)
            {
                _counts[item] = _counts.TryGetValue(item, out int count) ? count + 1 : 1;
            }
        }

        /// <summary>
        /// Get the count for a given element. Returns 0 if the element is not present.
        /// </summary>
        public int this[T key]
        {
            get => _counts.TryGetValue(key, out int count) ? count : 0;
            set => _counts[key] = value;
        }

        /// <summary>
        /// Return a list of the n most common elements and their counts.
        /// Elements with equal counts are ordered by their key if T implements IComparable, otherwise in arbitrary order.
        /// </summary>
        public System.Collections.Generic.List<(T, int)> MostCommon(int? n = null)
        {
            var ordered = _counts.OrderByDescending(kv => kv.Value);

            // Only apply ThenBy if T implements IComparable to avoid runtime exceptions
            IEnumerable<System.Collections.Generic.KeyValuePair<T, int>> sorted;
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)) || typeof(IComparable).IsAssignableFrom(typeof(T)))
            {
                sorted = ordered.ThenBy(kv => kv.Key);
            }
            else
            {
                sorted = ordered;
            }

            if (n.HasValue)
            {
                sorted = sorted.Take(n.Value);
            }

            return sorted.Select(kv => (kv.Key, kv.Value)).ToList();
        }

        /// <summary>
        /// Elements are returned in arbitrary order. Each element is repeated count times.
        /// </summary>
        public IEnumerable<T> Elements()
        {
            foreach (var kvp in _counts)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    yield return kvp.Key;
                }
            }
        }

        /// <summary>
        /// Update counts from an iterable or another mapping.
        /// </summary>
        public void Update(IEnumerable<T> iterable)
        {
            foreach (var item in iterable)
            {
                _counts[item] = _counts.TryGetValue(item, out int count) ? count + 1 : 1;
            }
        }
    }

    /// <summary>
    /// Dictionary with default values for missing keys.
    /// </summary>
    public class DefaultDict<TKey, TValue> where TKey : notnull
    {
        private readonly System.Collections.Generic.Dictionary<TKey, TValue> _dict;
        private readonly Func<TValue> _defaultFactory;

        public DefaultDict(Func<TValue> defaultFactory)
        {
            _dict = new System.Collections.Generic.Dictionary<TKey, TValue>();
            _defaultFactory = defaultFactory ?? throw new TypeError("default_factory cannot be None");
        }

        /// <summary>
        /// Get or set the value for a given key. If the key is not present, the default factory is called.
        /// </summary>
        public TValue this[TKey key]
        {
            get
            {
                if (!_dict.TryGetValue(key, out TValue? value))
                {
                    value = _defaultFactory();
                    _dict[key] = value;
                }
                return value;
            }
            set => _dict[key] = value;
        }

        /// <summary>
        /// Get the value for a key, or return a default value if the key is not present.
        /// </summary>
        public TValue Get(TKey key, TValue defaultValue = default!)
        {
            return _dict.TryGetValue(key, out TValue? value) ? value : defaultValue;
        }

        /// <summary>
        /// Check if the dictionary contains a key.
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            return _dict.ContainsKey(key);
        }

        public IEnumerable<TKey> Keys => _dict.Keys;
        public IEnumerable<TValue> Values => _dict.Values;
    }

    /// <summary>
    /// Module exports for collections.
    /// </summary>
    [SharpyModule("collections")]
    public static class Collections
    {
        // Re-export the classes for convenience
        public static Type DequeType => typeof(Deque<>);
        public static Type CounterType => typeof(Counter<>);
        public static Type DefaultDictType => typeof(DefaultDict<,>);
    }
}
