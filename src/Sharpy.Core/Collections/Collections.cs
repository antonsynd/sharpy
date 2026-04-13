using System.Linq;
using System.Collections.Generic;
using System;

namespace Sharpy
{
    /// <summary>
    /// A deque (double-ended queue) is a generalization of stacks and queues
    /// that supports adding and removing elements from either end.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <example>
    /// <code>
    /// d = deque([1, 2, 3])
    /// d.append(4)        # deque([1, 2, 3, 4])
    /// d.appendleft(0)    # deque([0, 1, 2, 3, 4])
    /// d.pop()            # 4
    /// d.popleft()        # 0
    /// </code>
    /// </example>
    [SharpyModuleType("collections", "Deque")]
    public class Deque<T> : IReadOnlyCollection<T>
    {
        private readonly System.Collections.Generic.LinkedList<T> _list;

        /// <summary>Create an empty deque.</summary>
        public Deque()
        {
            _list = new System.Collections.Generic.LinkedList<T>();
        }

        /// <summary>Create a deque initialized with elements from the iterable.</summary>
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

        /// <summary>Return an enumerator over the deque elements.</summary>
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _list.GetEnumerator();
    }

    /// <summary>
    /// A Counter is a dict subclass for counting hashable objects.
    /// </summary>
    /// <typeparam name="T">The element type to count.</typeparam>
    /// <example>
    /// <code>
    /// c = Counter(["a", "b", "a", "c", "a"])
    /// c["a"]              # 3
    /// c.most_common(2)    # [("a", 3), ("b", 1)]
    /// </code>
    /// </example>
    [SharpyModuleType("collections", "Counter")]
    public class Counter<T> where T : notnull
    {
        private readonly System.Collections.Generic.Dictionary<T, int> _counts;

        /// <summary>Create an empty counter.</summary>
        public Counter()
        {
            _counts = new System.Collections.Generic.Dictionary<T, int>();
        }

        /// <summary>Create a counter from elements in the iterable.</summary>
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
        public Sharpy.List<(T, int)> MostCommon(int? n = null)
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

            return new Sharpy.List<(T, int)>(sorted.Select(kv => (kv.Key, kv.Value)));
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

        /// <summary>
        /// Subtract counts. Elements are subtracted from an iterable.
        /// Counts can go below zero.
        /// </summary>
        public void Subtract(IEnumerable<T> iterable)
        {
            foreach (var item in iterable)
            {
                _counts[item] = _counts.TryGetValue(item, out int count) ? count - 1 : -1;
            }
        }

        /// <summary>
        /// Subtract counts from another Counter.
        /// Counts can go below zero.
        /// </summary>
        public void Subtract(Counter<T> other)
        {
            foreach (var kvp in other._counts)
            {
                _counts[kvp.Key] = _counts.TryGetValue(kvp.Key, out int count)
                    ? count - kvp.Value
                    : -kvp.Value;
            }
        }

        /// <summary>
        /// Return a shallow copy of this counter.
        /// </summary>
        public Counter<T> Copy()
        {
            var result = new Counter<T>();
            foreach (var kvp in _counts)
            {
                result._counts[kvp.Key] = kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// Return the sum of all counts.
        /// </summary>
        public int Total()
        {
            int sum = 0;
            foreach (var kvp in _counts)
            {
                sum += kvp.Value;
            }
            return sum;
        }

        /// <summary>
        /// Remove all elements from the counter.
        /// </summary>
        public void Clear()
        {
            _counts.Clear();
        }

        /// <summary>The keys of the counter.</summary>
        public IEnumerable<T> Keys => _counts.Keys;

        /// <summary>Check if the counter contains a key.</summary>
        public bool ContainsKey(T key) => _counts.ContainsKey(key);

        /// <summary>
        /// Combine two counters by adding counts.
        /// </summary>
        public static Counter<T> operator +(Counter<T> left, Counter<T> right)
        {
            var result = left.Copy();
            foreach (var kvp in right._counts)
            {
                result._counts[kvp.Key] = result._counts.TryGetValue(kvp.Key, out int count)
                    ? count + kvp.Value
                    : kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// Subtract counts, dropping zero and negative results.
        /// </summary>
        public static Counter<T> operator -(Counter<T> left, Counter<T> right)
        {
            var result = new Counter<T>();
            foreach (var kvp in left._counts)
            {
                int rightCount = right._counts.TryGetValue(kvp.Key, out int rc) ? rc : 0;
                int diff = kvp.Value - rightCount;
                if (diff > 0)
                {
                    result._counts[kvp.Key] = diff;
                }
            }
            return result;
        }

        /// <summary>
        /// Union: max of corresponding counts.
        /// </summary>
        public static Counter<T> operator |(Counter<T> left, Counter<T> right)
        {
            var result = new Counter<T>();
            var allKeys = new System.Collections.Generic.HashSet<T>(left._counts.Keys);
            foreach (var key in right._counts.Keys)
            {
                allKeys.Add(key);
            }
            foreach (var key in allKeys)
            {
                int leftCount = left._counts.TryGetValue(key, out int lc) ? lc : 0;
                int rightCount = right._counts.TryGetValue(key, out int rc) ? rc : 0;
                int max = leftCount > rightCount ? leftCount : rightCount;
                if (max > 0)
                {
                    result._counts[key] = max;
                }
            }
            return result;
        }

        /// <summary>
        /// Intersection: min of corresponding counts, dropping zero and negative.
        /// </summary>
        public static Counter<T> operator &(Counter<T> left, Counter<T> right)
        {
            var result = new Counter<T>();
            foreach (var kvp in left._counts)
            {
                if (right._counts.TryGetValue(kvp.Key, out int rightCount))
                {
                    int min = kvp.Value < rightCount ? kvp.Value : rightCount;
                    if (min > 0)
                    {
                        result._counts[kvp.Key] = min;
                    }
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Dictionary with default values for missing keys.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <example>
    /// <code>
    /// dd = defaultdict(list)
    /// dd["key"].append(1)    # automatically creates list for missing key
    /// dd["key"]              # [1]
    /// </code>
    /// </example>
    [SharpyModuleType("collections", "DefaultDict")]
    public class DefaultDict<TKey, TValue> where TKey : notnull
    {
        private readonly System.Collections.Generic.Dictionary<TKey, TValue> _dict;
        private readonly Func<TValue> _defaultFactory;

        /// <summary>Create a defaultdict with the given factory for missing keys.</summary>
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

        /// <summary>The keys of the dictionary.</summary>
        public IEnumerable<TKey> Keys => _dict.Keys;
        /// <summary>The values of the dictionary.</summary>
        public IEnumerable<TValue> Values => _dict.Values;

        /// <summary>The default factory function used for missing keys.</summary>
        public Func<TValue> DefaultFactory => _defaultFactory;

        /// <summary>
        /// Return a shallow copy of this defaultdict, preserving the default factory.
        /// </summary>
        public DefaultDict<TKey, TValue> Copy()
        {
            var result = new DefaultDict<TKey, TValue>(_defaultFactory);
            foreach (var kvp in _dict)
            {
                result._dict[kvp.Key] = kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// Remove all items from the defaultdict.
        /// </summary>
        public void Clear()
        {
            _dict.Clear();
        }

        /// <summary>
        /// Remove the specified key and return its value.
        /// Raises <see cref="KeyError"/> if the key is not found.
        /// </summary>
        public TValue Pop(TKey key)
        {
            if (_dict.TryGetValue(key, out TValue? value))
            {
                _dict.Remove(key);
                return value;
            }

            throw new KeyError(Builtins.Repr(key));
        }

        /// <summary>
        /// Remove the specified key and return its value.
        /// If the key is not found, return <paramref name="defaultValue"/>.
        /// </summary>
        public TValue Pop(TKey key, TValue defaultValue)
        {
            if (_dict.TryGetValue(key, out TValue? value))
            {
                _dict.Remove(key);
                return value;
            }

            return defaultValue;
        }

        /// <summary>
        /// Return a list of (key, value) tuples.
        /// </summary>
        public List<(TKey, TValue)> Items()
        {
            var items = new List<(TKey, TValue)>();
            foreach (var kvp in _dict)
            {
                items.Add((kvp.Key, kvp.Value));
            }
            return items;
        }

        /// <summary>
        /// Update the defaultdict with key-value pairs from another dictionary.
        /// </summary>
        public void Update(IDictionary<TKey, TValue> other)
        {
            foreach (var kvp in other)
            {
                _dict[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Update the defaultdict with key-value pairs from an iterable of tuples.
        /// </summary>
        public void Update(IEnumerable<(TKey, TValue)> other)
        {
            foreach (var (key, value) in other)
            {
                _dict[key] = value;
            }
        }

        /// <summary>
        /// If <paramref name="key"/> is in the dictionary, return its value.
        /// If not, insert <paramref name="key"/> with <paramref name="defaultValue"/>
        /// and return <paramref name="defaultValue"/>.
        /// </summary>
        public TValue SetDefault(TKey key, TValue defaultValue)
        {
            if (_dict.TryGetValue(key, out TValue? value))
            {
                return value;
            }

            _dict[key] = defaultValue;
            return defaultValue;
        }

        /// <summary>
        /// Remove and return a (key, value) pair. If <paramref name="last"/> is true,
        /// pairs are returned in LIFO order; otherwise in FIFO order.
        /// </summary>
        /// <exception cref="KeyError">Thrown if the defaultdict is empty.</exception>
        public (TKey, TValue) PopItem(bool last = false)
        {
            if (_dict.Count == 0)
            {
                throw new KeyError("dictionary is empty");
            }

            var pair = last ? _dict.Last() : _dict.First();
            _dict.Remove(pair.Key);
            return (pair.Key, pair.Value);
        }

        /// <summary>
        /// Removes the item with the specified key from the defaultdict.
        /// </summary>
        /// <exception cref="KeyError">Thrown if the key does not exist.</exception>
        public void Remove(TKey key)
        {
            if (!_dict.Remove(key))
            {
                throw new KeyError(Builtins.Repr(key));
            }
        }

        /// <summary>Convert to a standard .NET Dictionary.</summary>
        public Dictionary<TKey, TValue> ToDictionary()
        {
            return new Dictionary<TKey, TValue>(_dict);
        }

        /// <summary>The number of items in the defaultdict.</summary>
        public int Count => _dict.Count;
    }

    /// <summary>
    /// Module exports for collections.
    /// </summary>
    public static partial class Collections
    {
        /// <summary>The Deque type.</summary>
        public static Type DequeType => typeof(Deque<>);
        /// <summary>The Counter type.</summary>
        public static Type CounterType => typeof(Counter<>);
        /// <summary>The DefaultDict type.</summary>
        public static Type DefaultDictType => typeof(DefaultDict<,>);
        /// <summary>The OrderedDict type.</summary>
        public static Type OrderedDictType => typeof(OrderedDict<,>);
        /// <summary>The ChainMap type.</summary>
        public static Type ChainMapType => typeof(ChainMap<,>);
    }
}
