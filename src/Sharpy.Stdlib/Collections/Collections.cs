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
    /// <remarks>
    /// Implements <see cref="ISized"/> (<c>__len__</c> → <c>len(d)</c> and truth testing:
    /// <c>if d:</c> is false for an empty deque). <see cref="IReadOnlyCollection{T}"/> alone gave
    /// <c>len()</c> a count but left every truth position refused (SPY0220), because the truth
    /// classifier reads the dunder table's spelling, <see cref="ISized"/> (#1972).
    /// </remarks>
    [SharpyModuleType("collections", "Deque", MessageName = "collections.Deque")]
    public class Deque<T> : IReadOnlyCollection<T>, ISized
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
        /// The number of elements — <c>len(d)</c>. Explicit, as <c>List&lt;T&gt;</c> spells it, so the
        /// public name <c>Count</c> is python's <c>d.count(x)</c> method: a public <c>Count</c>
        /// property surfaced a non-python <c>d.count</c> attribute and sent <c>d.count(x)</c> to LINQ's
        /// <c>Count(predicate)</c> extension (SPY0220, #2107).
        /// </summary>
        int IReadOnlyCollection<T>.Count => _list.Count;

        /// <inheritdoc/>
        int ISized.Count => _list.Count;

        /// <summary>
        /// Return the number of elements equal to x.
        /// </summary>
        /// <example>
        /// <code>
        /// d = deque([1, 5, 1])
        /// d.count(1)    # 2
        /// </code>
        /// </example>
        public int Count(T x)
        {
            var comparer = EqualityComparer<T>.Default;
            int n = 0;
            foreach (var item in _list)
            {
                if (comparer.Equals(item, x))
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>
        /// Return the position of the first element equal to x, searching the slice
        /// <c>[start:stop]</c> (python's slice clamping: a negative bound counts from the right, and
        /// out-of-range bounds clamp to the deque). The position is relative to the whole deque.
        /// Raises <c>ValueError: x is not in deque</c> when there is no such element.
        /// </summary>
        /// <param name="x">The value to search for.</param>
        /// <param name="start">Start of the searched slice (default 0).</param>
        /// <param name="stop">End of the searched slice (default: the end of the deque).</param>
        /// <example>
        /// <code>
        /// d = deque([1, 5, 1, 3])
        /// d.index(1)       # 0
        /// d.index(1, 1)    # 2
        /// </code>
        /// </example>
        public int Index(T x, int start = 0, int? stop = null)
        {
            int count = _list.Count;
            int lo = ClampSliceBound(start, count);
            int hi = stop is int s ? ClampSliceBound(s, count) : count;
            var comparer = EqualityComparer<T>.Default;
            var node = _list.First;
            for (int i = 0; node != null && i < hi; i++, node = node.Next)
            {
                if (i >= lo && comparer.Equals(node.Value, x))
                {
                    return i;
                }
            }

            throw new ValueError(Builtins.Repr(x) + " is not in deque");
        }

        /// <summary>
        /// Remove the first element equal to x. Raises <c>ValueError: x is not in deque</c> when
        /// there is no such element.
        /// </summary>
        /// <example>
        /// <code>
        /// d = deque([1, 5, 1])
        /// d.remove(1)    # deque([5, 1])
        /// </code>
        /// </example>
        public void Remove(T x)
        {
            if (!_list.Remove(x))
            {
                throw new ValueError(Builtins.Repr(x) + " is not in deque");
            }
        }

        /// <summary>
        /// Insert x before position i. As python's unbounded deque does, i is clamped like
        /// <c>list.insert</c>: a negative i counts from the right, and an out-of-range i inserts at
        /// the nearer end.
        /// </summary>
        /// <example>
        /// <code>
        /// d = deque([1, 2, 3])
        /// d.insert(1, 9)    # deque([1, 9, 2, 3])
        /// </code>
        /// </example>
        public void Insert(int i, T x)
        {
            int count = _list.Count;
            int at = ClampSliceBound(i, count);
            if (at == count)
            {
                _list.AddLast(x);
                return;
            }

            _list.AddBefore(NodeAt(at), x);
        }

        /// <summary>
        /// Reverse the elements of the deque in place and return None.
        /// </summary>
        // Without this member `d.reverse()` bound LINQ's Enumerable.Reverse extension, which returns a
        // reversed copy and leaves the deque unchanged — a silent wrong (#2107).
        public void Reverse()
        {
            var items = new T[_list.Count];
            _list.CopyTo(items, 0);
            _list.Clear();
            foreach (var item in items)
            {
                _list.AddFirst(item);
            }
        }

        /// <summary>
        /// Rotate the deque n steps to the right (to the left when n is negative). Rotating one step
        /// to the right is <c>d.appendleft(d.pop())</c>.
        /// </summary>
        /// <example>
        /// <code>
        /// d = deque([1, 2, 3, 4, 5])
        /// d.rotate(2)     # deque([4, 5, 1, 2, 3])
        /// d.rotate(-1)    # deque([5, 1, 2, 3, 4])
        /// </code>
        /// </example>
        public void Rotate(int n = 1)
        {
            int count = _list.Count;
            if (count <= 1)
            {
                return;
            }

            int right = Builtins.FloorMod(n, count);
            if (right <= count / 2)
            {
                for (int k = 0; k < right; k++)
                {
                    var last = _list.Last!;
                    _list.RemoveLast();
                    _list.AddFirst(last);
                }

                return;
            }

            for (int k = 0; k < count - right; k++)
            {
                var first = _list.First!;
                _list.RemoveFirst();
                _list.AddLast(first);
            }
        }

        /// <summary>
        /// Return a shallow copy of the deque.
        /// </summary>
        public Deque<T> Copy() => new Deque<T>(_list);

        /// <summary>
        /// A slice bound or insertion position normalized as python normalizes them: a negative value
        /// counts from the right, then the result is clamped to <c>[0, count]</c>.
        /// </summary>
        private static int ClampSliceBound(int i, int count)
        {
            if (i < 0)
            {
                i += count;
            }

            return i < 0 ? 0 : (i > count ? count : i);
        }

        /// <summary>
        /// <c>d[i]</c> and <c>d[i] = x</c>, as python indexes a deque (#2035): a negative index counts
        /// from the right, and an index outside the deque raises <c>IndexError: deque index out of
        /// range</c>. The store is a linked list, so a middle index walks from the nearer end — O(n),
        /// as python's own middle indexing is.
        /// </summary>
        public T this[int index]
        {
            get => NodeAt(index).Value;
            set => NodeAt(index).Value = value;
        }

        private System.Collections.Generic.LinkedListNode<T> NodeAt(int index)
        {
            int count = _list.Count;
            if (index < 0)
            {
                index += count;
            }

            if (index < 0 || index >= count)
            {
                throw new IndexError("deque index out of range");
            }

            if (index <= count / 2)
            {
                var node = _list.First!;
                for (int i = 0; i < index; i++)
                {
                    node = node.Next!;
                }

                return node;
            }

            var fromEnd = _list.Last!;
            for (int i = count - 1; i > index; i--)
            {
                fromEnd = fromEnd.Previous!;
            }

            return fromEnd;
        }

        /// <summary>Return an enumerator over the deque elements.</summary>
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        /// <summary>
        /// Python's <c>repr</c> of the deque: <c>deque([1, 2])</c>, and <c>deque([])</c> when empty.
        /// There is no <c>maxlen</c> suffix — Sharpy's deque is unbounded, and CPython prints none
        /// for <c>maxlen=None</c>.
        /// </summary>
        public override string ToString() => "deque(" + new List<T>(_list).ToString() + ")";

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
        /// Elements with equal counts keep first-seen order, as CPython's stable sort does
        /// (<c>Counter("cba").most_common()</c> is <c>[('c', 1), ('b', 1), ('a', 1)]</c>, #1979).
        /// </summary>
        public Sharpy.List<(T, int)> MostCommon(int? n = null)
        {
            IEnumerable<System.Collections.Generic.KeyValuePair<T, int>> sorted = ByCountDescending();

            if (n.HasValue)
            {
                sorted = sorted.Take(n.Value);
            }

            return new Sharpy.List<(T, int)>(sorted.Select(kv => (kv.Key, kv.Value)));
        }

        /// <summary>
        /// The pairs in most-common order — the one ordering <see cref="MostCommon"/> and
        /// <see cref="ToString"/> share. <c>OrderByDescending</c> is a stable sort and the backing
        /// dictionary enumerates in first-seen order (entries are never removed, only cleared), so
        /// ties keep first-seen order.
        /// </summary>
        private IEnumerable<System.Collections.Generic.KeyValuePair<T, int>> ByCountDescending()
            => _counts.OrderByDescending(kv => kv.Value);

        /// <summary>
        /// Python's <c>repr(c)</c>/<c>str(c)</c>: <c>Counter({...})</c> in most-common order, or
        /// <c>Counter()</c> when empty (CPython 3.12). The braces are <see cref="Dict{K, V}"/>'s own
        /// repr, so there is one spelling of the mapping rule.
        /// </summary>
        public override string ToString()
            => _counts.Count == 0 ? "Counter()" : "Counter(" + new Dict<T, int>(ByCountDescending()).ToString() + ")";

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
        /// Return the value for <paramref name="key"/> if present, otherwise None. A missing key does
        /// NOT run the factory (python's <c>get</c> does not).
        /// </summary>
        // Two overloads, as Dict<K, V> spells them, rather than one defaulted TValue: a
        // `TValue defaultValue = default!` (or `TValue? … = default`, the same parameter for a
        // value-type TValue) returned 0 for a missing key of a defaultdict[str, int] where python
        // returns None (#2054).
        public Optional<TValue> Get(TKey key)
        {
            return _dict.TryGetValue(key, out TValue value) ? Optional<TValue>.Some(value) : Optional<TValue>.None;
        }

        /// <summary>
        /// Return the value for <paramref name="key"/> if present, otherwise <paramref name="defaultValue"/>.
        /// A missing key does NOT run the factory.
        /// </summary>
        public TValue Get(TKey key, TValue defaultValue)
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
        /// The keys of the dictionary. Python: <c>d.keys()</c>. A live view: later mutations of the
        /// defaultdict are reflected.
        /// </summary>
        public DictKeyView<TKey, TValue> Keys() => _dict.Keys();

        /// <summary>
        /// The values of the dictionary. Python: <c>d.values()</c>. A live view: later mutations of
        /// the defaultdict are reflected.
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
        /// The (key, value) pairs of the dictionary. Python: <c>d.items()</c>. A live view, like
        /// <see cref="Keys"/> and <see cref="Values"/>: later mutations of the defaultdict are reflected.
        /// </summary>
        public DictItemsView<TKey, TValue> Items() => _dict.Items();

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

        /// <summary>
        /// Python's <c>repr(dd)</c>/<c>str(dd)</c>: <c>defaultdict(&lt;factory&gt;, {...})</c>, the
        /// pairs rendered by the composed <see cref="Dict{K, V}"/>'s own repr.
        /// </summary>
        /// <remarks>
        /// Documented deviation (owner ruling R-BN, #1968): CPython prints the factory's repr —
        /// <c>defaultdict(&lt;class 'int'&gt;, {'n': 1})</c> — but a Sharpy factory is a .NET delegate
        /// with no Python-meaningful repr, so the factory slot is always the literal placeholder
        /// <c>&lt;factory&gt;</c>. The mapping part matches CPython exactly.
        /// </remarks>
        public override string ToString() => "defaultdict(<factory>, " + _dict.ToString() + ")";

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
