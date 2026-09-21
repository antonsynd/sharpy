using System.Linq;
using System.Collections;
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
    /// <remarks>
    /// Implements <see cref="ISized"/> (<c>__len__</c> → <c>len(c)</c>, the number of distinct
    /// elements) and <see cref="IEnumerable{T}"/> over the KEYS in first-seen order (<c>__iter__</c>
    /// → <c>for k in c</c>, <c>list(c)</c>). Keys are the only generic <c>IEnumerable</c> the type
    /// exposes so <c>list(c)</c> binds <c>Builtins.List&lt;T&gt;(IEnumerable&lt;T&gt;)</c> (#1933).
    /// </remarks>
    [SharpyModuleType("collections", "Counter")]
    public class Counter<T> : ISized, IEnumerable<T>, IEquatable<Counter<T>> where T : notnull
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
        /// Create a counter from a string, counting its code units — Python's <c>Counter("abca")</c>
        /// (only meaningful for <c>Counter[str]</c>). Iterates via <see cref="StringHelpers.Iterate"/>
        /// so each element is a one-code-unit string, matching Sharpy's string iteration.
        /// </summary>
        public Counter(string s)
        {
            _counts = new System.Collections.Generic.Dictionary<T, int>();
            foreach (var codeUnit in StringHelpers.Iterate(s))
            {
                var key = (T)(object)codeUnit;
                _counts[key] = _counts.TryGetValue(key, out int count) ? count + 1 : 1;
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

        /// <summary>
        /// The keys of the counter. Python: <c>c.keys()</c>. Returns a copy, not a live view.
        /// </summary>
        // A METHOD, not a property, because `c.keys` without the call must not be a value (#1391).
        // While it was a property the #1294 property-read rule typed `c.keys` as `list[str]`, so
        // `c.keys.append("c")` SUCCEEDED — where CPython raises AttributeError, `c.keys` there being
        // a bound method. Sibling mapping types already disagreed: ChainMap.Keys() and
        // OrderedDict.Keys() were methods, so one spelling meant different things by receiver.
        // Kept out of <remarks> deliberately — docs/stdlib is generated from XML doc comments, and
        // this paragraph is implementation history, not something a stdlib reader needs.
        public List<T> Keys() => new List<T>(_counts.Keys);

        /// <summary>
        /// The counts of the counter, in first-seen key order. Python: <c>c.values()</c>.
        /// </summary>
        public List<int> Values() => new List<int>(_counts.Values);

        /// <summary>
        /// The (element, count) pairs, in first-seen key order. Python: <c>c.items()</c>.
        /// </summary>
        public List<(T, int)> Items() => new List<(T, int)>(_counts.Select(kv => (kv.Key, kv.Value)));

        /// <summary>
        /// The number of distinct elements. Python's <c>len(c)</c>; ISized's <c>__len__</c>.
        /// </summary>
        public int Count => _counts.Count;

        /// <summary>Iterate the distinct elements in first-seen order (Python's <c>__iter__</c>).</summary>
        public IEnumerator<T> GetEnumerator() => _counts.Keys.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Check if the counter contains a key.</summary>
        public bool ContainsKey(T key) => _counts.ContainsKey(key);

        /// <summary>
        /// Check if the counter contains a key (alias for ContainsKey).
        /// Used by the <c>in</c> operator.
        /// </summary>
        public bool Contains(T key) => ContainsKey(key);

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

        /// <summary>The (element, count) pairs, for equality comparison by sibling mappings.</summary>
        internal IEnumerable<System.Collections.Generic.KeyValuePair<T, int>> PairsForEquality()
            => _counts.Select(kv => new System.Collections.Generic.KeyValuePair<T, int>(kv.Key, kv.Value));

        // ── Equality (Python __eq__): a Counter compares by contents like a dict of counts.
        //    Declared as operator ==/!= so the checker discovers them through the CLR op_Equality
        //    path Dict<K,V> uses (Dict.cs:286) — no compiler arm (#1933). A Counter is a K→int
        //    mapping, so its cross-stdlib pairs pin the other mapping's value type to int.

        /// <summary>Content equality with another Counter.</summary>
        public bool Equals(Counter<T>? other)
            => other is not null && MappingEquality.PairwiseEquals(Count, PairsForEquality(), other.Count, other.PairsForEquality());

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Counter<T> other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Count;

        /// <summary>Counter == Counter — pairwise on counts.</summary>
        public static bool operator ==(Counter<T>? left, Counter<T>? right)
            => left is null ? right is null : left.Equals(right);

        /// <summary>Counter != Counter.</summary>
        public static bool operator !=(Counter<T>? left, Counter<T>? right) => !(left == right);

        /// <summary>Counter == dict — pairwise, matching CPython.</summary>
        public static bool operator ==(Counter<T>? left, Dict<T, int>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left.PairsForEquality(), right.Count, right);

        /// <summary>Counter != dict.</summary>
        public static bool operator !=(Counter<T>? left, Dict<T, int>? right) => !(left == right);

        /// <summary>dict == Counter — pairwise, delegating to the symmetric overload.</summary>
        public static bool operator ==(Dict<T, int>? left, Counter<T>? right) => right == left;

        /// <summary>dict != Counter.</summary>
        public static bool operator !=(Dict<T, int>? left, Counter<T>? right) => !(left == right);

        /// <summary>Counter == defaultdict — pairwise cross-stdlib pair (#1933).</summary>
        public static bool operator ==(Counter<T>? left, DefaultDict<T, int>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left.PairsForEquality(), right.Count, right.PairsForEquality());

        /// <summary>Counter != defaultdict.</summary>
        public static bool operator !=(Counter<T>? left, DefaultDict<T, int>? right) => !(left == right);

        /// <summary>Counter == OrderedDict — pairwise cross-stdlib pair (#1933).</summary>
        public static bool operator ==(Counter<T>? left, OrderedDict<T, int>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left.PairsForEquality(), right.Count, right.ItemsList);

        /// <summary>Counter != OrderedDict.</summary>
        public static bool operator !=(Counter<T>? left, OrderedDict<T, int>? right) => !(left == right);

        /// <summary>Counter == ChainMap — pairwise cross-stdlib pair (#1933).</summary>
        public static bool operator ==(Counter<T>? left, ChainMap<T, int>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left.PairsForEquality(), right.Count, right.PairsForEquality());

        /// <summary>Counter != ChainMap.</summary>
        public static bool operator !=(Counter<T>? left, ChainMap<T, int>? right) => !(left == right);
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
    /// <remarks>
    /// Implements <see cref="ISized"/> (<c>__len__</c> → <c>len(dd)</c>) and
    /// <see cref="IEnumerable{T}"/> over the KEYS in insertion order (<c>__iter__</c> →
    /// <c>for k in dd</c>, <c>list(dd)</c>), delegating both to the composed <see cref="Dict{K, V}"/>.
    /// Keys are the only generic <c>IEnumerable</c> the type exposes so <c>list(dd)</c> binds
    /// <c>Builtins.List&lt;TKey&gt;(IEnumerable&lt;TKey&gt;)</c> (#1933).
    /// </remarks>
    [SharpyModuleType("collections", "DefaultDict")]
    public class DefaultDict<TKey, TValue> : ISized, IEnumerable<TKey>, IEquatable<DefaultDict<TKey, TValue>> where TKey : notnull
    {
        private readonly Dict<TKey, TValue> _dict;
        private readonly Func<TValue> _defaultFactory;

        /// <summary>Create a defaultdict with the given factory for missing keys.</summary>
        public DefaultDict(Func<TValue> defaultFactory)
        {
            _dict = new Dict<TKey, TValue>();
            _defaultFactory = defaultFactory ?? throw new TypeError("default_factory cannot be None");
        }

        /// <summary>
        /// Get or set the value for a given key. If the key is not present, the default factory is called.
        /// </summary>
        public TValue this[TKey key]
        {
            get
            {
                if (!_dict.TryGetValue(key, out TValue value))
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
            return _dict.TryGetValue(key, out TValue value) ? value : defaultValue;
        }

        /// <summary>
        /// Check if the dictionary contains a key.
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            return _dict.ContainsKey(key);
        }

        /// <summary>
        /// Check if the dictionary contains a key (alias for ContainsKey).
        /// Used by the <c>in</c> operator: <c>"x" in d</c> → <c>d.Contains("x")</c>.
        /// </summary>
        public bool Contains(TKey key) => ContainsKey(key);

        /// <summary>
        /// The keys of the dictionary. Python: <c>d.keys()</c>. Returns a copy, not a live view.
        /// </summary>
        public DictKeyView<TKey, TValue> Keys() => _dict.Keys();

        /// <summary>
        /// The values of the dictionary. Python: <c>d.values()</c>. Returns a copy, not a live view.
        /// </summary>
        public DictValuesView<TKey, TValue> Values() => _dict.Values();

        /// <summary>The default factory function used for missing keys.</summary>
        public Func<TValue> DefaultFactory => _defaultFactory;

        /// <summary>
        /// Return a shallow copy of this defaultdict, preserving the default factory.
        /// </summary>
        public DefaultDict<TKey, TValue> Copy()
        {
            var result = new DefaultDict<TKey, TValue>(_defaultFactory);
            foreach (var key in _dict)
            {
                result._dict[key] = _dict[key];
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
            return _dict.Pop(key);
        }

        /// <summary>
        /// Remove the specified key and return its value.
        /// If the key is not found, return <paramref name="defaultValue"/>.
        /// </summary>
        public TValue Pop(TKey key, TValue defaultValue)
        {
            return _dict.Pop(key, defaultValue);
        }

        /// <summary>
        /// Return a list of (key, value) tuples.
        /// </summary>
        public List<(TKey, TValue)> Items()
        {
            var items = new List<(TKey, TValue)>();
            foreach (var (k, v) in _dict.Items())
            {
                items.Add((k, v));
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
            _dict.Update(other);
        }

        /// <summary>
        /// If <paramref name="key"/> is in the dictionary, return its value.
        /// If not, insert <paramref name="key"/> with <paramref name="defaultValue"/>
        /// and return <paramref name="defaultValue"/>.
        /// </summary>
        public TValue SetDefault(TKey key, TValue defaultValue)
        {
            return _dict.SetDefault(key, defaultValue);
        }

        /// <summary>
        /// Remove and return a (key, value) pair. If <paramref name="last"/> is true,
        /// pairs are returned in LIFO order; otherwise in FIFO order.
        /// </summary>
        /// <exception cref="KeyError">Thrown if the defaultdict is empty.</exception>
        public (TKey, TValue) PopItem(bool last = true)
        {
            return _dict.PopItem(last);
        }

        /// <summary>
        /// Removes the item with the specified key from the defaultdict.
        /// </summary>
        /// <exception cref="KeyError">Thrown if the key does not exist.</exception>
        public void Remove(TKey key)
        {
            _dict.Remove(key);
        }

        /// <summary>Convert to a standard .NET Dictionary.</summary>
        public Dictionary<TKey, TValue> ToDictionary()
        {
            return _dict.ToDictionary();
        }

        /// <summary>The number of items in the defaultdict.</summary>
        public int Count => _dict.Count;

        /// <summary>Iterate the keys in insertion order (Python's <c>__iter__</c>).</summary>
        public IEnumerator<TKey> GetEnumerator() => _dict.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>The (key, value) pairs, for equality comparison by sibling mappings.</summary>
        internal IEnumerable<KeyValuePair<TKey, TValue>> PairsForEquality() => _dict;

        // ── Equality (Python __eq__): a defaultdict compares by contents like a plain dict — the
        //    default factory is not part of equality. Declared as operator ==/!= so the checker
        //    discovers them through the CLR op_Equality path Dict<K,V> uses (Dict.cs:286) — no arm (#1933).

        /// <summary>Content equality with another defaultdict.</summary>
        public bool Equals(DefaultDict<TKey, TValue>? other)
            => other is not null && MappingEquality.PairwiseEquals(Count, PairsForEquality(), other.Count, other.PairsForEquality());

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is DefaultDict<TKey, TValue> other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Count;

        /// <summary>defaultdict == defaultdict — pairwise on contents.</summary>
        public static bool operator ==(DefaultDict<TKey, TValue>? left, DefaultDict<TKey, TValue>? right)
            => left is null ? right is null : left.Equals(right);

        /// <summary>defaultdict != defaultdict.</summary>
        public static bool operator !=(DefaultDict<TKey, TValue>? left, DefaultDict<TKey, TValue>? right) => !(left == right);

        /// <summary>defaultdict == dict — pairwise, matching CPython.</summary>
        public static bool operator ==(DefaultDict<TKey, TValue>? left, Dict<TKey, TValue>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left.PairsForEquality(), right.Count, right);

        /// <summary>defaultdict != dict.</summary>
        public static bool operator !=(DefaultDict<TKey, TValue>? left, Dict<TKey, TValue>? right) => !(left == right);

        /// <summary>dict == defaultdict — pairwise, delegating to the symmetric overload.</summary>
        public static bool operator ==(Dict<TKey, TValue>? left, DefaultDict<TKey, TValue>? right) => right == left;

        /// <summary>dict != defaultdict.</summary>
        public static bool operator !=(Dict<TKey, TValue>? left, DefaultDict<TKey, TValue>? right) => !(left == right);

        /// <summary>defaultdict == OrderedDict — pairwise cross-stdlib pair (#1933).</summary>
        public static bool operator ==(DefaultDict<TKey, TValue>? left, OrderedDict<TKey, TValue>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left.PairsForEquality(), right.Count, right.ItemsList);

        /// <summary>defaultdict != OrderedDict.</summary>
        public static bool operator !=(DefaultDict<TKey, TValue>? left, OrderedDict<TKey, TValue>? right) => !(left == right);

        /// <summary>defaultdict == ChainMap — pairwise cross-stdlib pair (#1933).</summary>
        public static bool operator ==(DefaultDict<TKey, TValue>? left, ChainMap<TKey, TValue>? right)
            => left is null
                ? right is null
                : right is not null && MappingEquality.PairwiseEquals(left.Count, left.PairsForEquality(), right.Count, right.PairsForEquality());

        /// <summary>defaultdict != ChainMap.</summary>
        public static bool operator !=(DefaultDict<TKey, TValue>? left, ChainMap<TKey, TValue>? right) => !(left == right);
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
