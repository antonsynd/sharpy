using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Heap queue algorithm (priority queue), similar to Python's heapq module.
    /// Implements a min-heap using a list as the underlying storage.
    /// </summary>
    public static partial class Heapq
    {
        /// <summary>
        /// Push <paramref name="item"/> onto <paramref name="heap"/>, maintaining
        /// the min-heap invariant.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="heap">The heap list.</param>
        /// <param name="item">The item to push.</param>
        /// <example>
        /// <code>
        /// h = []
        /// heapq.heappush(h, 3)
        /// heapq.heappush(h, 1)
        /// heapq.heappush(h, 2)    # h[0] is now 1
        /// </code>
        /// </example>
        public static void Heappush<T>(IList<T> heap, T item) where T : IComparable<T>
        {
            heap.Add(item);
            SiftUp(heap, heap.Count - 1);
        }

        /// <summary>
        /// Pop and return the smallest item from <paramref name="heap"/>,
        /// maintaining the min-heap invariant.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="heap">The heap list.</param>
        /// <returns>The smallest element.</returns>
        /// <exception cref="IndexError">Thrown if the heap is empty.</exception>
        /// <example>
        /// <code>
        /// h = [1, 2, 3]
        /// heapq.heappop(h)    # returns 1
        /// </code>
        /// </example>
        public static T Heappop<T>(IList<T> heap) where T : IComparable<T>
        {
            if (heap.Count == 0)
            {
                throw new IndexError("index out of range");
            }

            int lastIdx = heap.Count - 1;
            T result = heap[0];

            if (lastIdx > 0)
            {
                heap[0] = heap[lastIdx];
                heap.RemoveAt(lastIdx);
                SiftDown(heap, 0);
            }
            else
            {
                heap.RemoveAt(0);
            }

            return result;
        }

        /// <summary>
        /// Transform <paramref name="x"/> into a min-heap, in-place, in O(n) time
        /// using Floyd's algorithm.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="x">The list to heapify.</param>
        /// <example>
        /// <code>
        /// x = [5, 3, 1, 4, 2]
        /// heapq.heapify(x)    # x is now a valid min-heap
        /// </code>
        /// </example>
        public static void Heapify<T>(IList<T> x) where T : IComparable<T>
        {
            int n = x.Count;
            for (int i = n / 2 - 1; i >= 0; i--)
            {
                SiftDown(x, i);
            }
        }

        /// <summary>
        /// Pop and return the smallest item from <paramref name="heap"/>, then push
        /// <paramref name="item"/>. More efficient than separate pop and push.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="heap">The heap list.</param>
        /// <param name="item">The item to push after popping.</param>
        /// <returns>The smallest element that was in the heap.</returns>
        /// <exception cref="IndexError">Thrown if the heap is empty.</exception>
        public static T Heapreplace<T>(IList<T> heap, T item) where T : IComparable<T>
        {
            if (heap.Count == 0)
            {
                throw new IndexError("index out of range");
            }

            T result = heap[0];
            heap[0] = item;
            SiftDown(heap, 0);
            return result;
        }

        /// <summary>
        /// Push <paramref name="item"/> onto <paramref name="heap"/>, then pop and
        /// return the smallest item. More efficient than separate push and pop.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="heap">The heap list.</param>
        /// <param name="item">The item to push before popping.</param>
        /// <returns>The smallest element after the push.</returns>
        public static T Heappushpop<T>(IList<T> heap, T item) where T : IComparable<T>
        {
            if (heap.Count > 0 && heap[0].CompareTo(item) < 0)
            {
                T result = heap[0];
                heap[0] = item;
                SiftDown(heap, 0);
                return result;
            }

            return item;
        }

        /// <summary>
        /// Return the <paramref name="n"/> largest elements from
        /// <paramref name="iterable"/>, in descending order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="n">The number of largest elements to return.</param>
        /// <param name="iterable">The collection to search.</param>
        /// <returns>A list of the <paramref name="n"/> largest elements.</returns>
        /// <example>
        /// <code>
        /// heapq.nlargest(3, [3, 1, 4, 1, 5, 9, 2, 6])    # [9, 6, 5]
        /// </code>
        /// </example>
        public static Sharpy.List<T> Nlargest<T>(int n, IList<T> iterable) where T : IComparable<T>
        {
            if (n <= 0)
            {
                return new Sharpy.List<T>(new System.Collections.Generic.List<T>());
            }

            var sorted = new System.Collections.Generic.List<T>(iterable);
            sorted.Sort((a, b) => b.CompareTo(a));

            if (n >= sorted.Count)
            {
                return new Sharpy.List<T>(sorted);
            }

            return new Sharpy.List<T>(sorted.GetRange(0, n));
        }

        /// <summary>
        /// Return the <paramref name="n"/> smallest elements from
        /// <paramref name="iterable"/>, in ascending order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="n">The number of smallest elements to return.</param>
        /// <param name="iterable">The collection to search.</param>
        /// <returns>A list of the <paramref name="n"/> smallest elements.</returns>
        /// <example>
        /// <code>
        /// heapq.nsmallest(3, [3, 1, 4, 1, 5, 9, 2, 6])    # [1, 1, 2]
        /// </code>
        /// </example>
        public static Sharpy.List<T> Nsmallest<T>(int n, IList<T> iterable) where T : IComparable<T>
        {
            if (n <= 0)
            {
                return new Sharpy.List<T>(new System.Collections.Generic.List<T>());
            }

            var sorted = new System.Collections.Generic.List<T>(iterable);
            sorted.Sort();

            if (n >= sorted.Count)
            {
                return new Sharpy.List<T>(sorted);
            }

            return new Sharpy.List<T>(sorted.GetRange(0, n));
        }

        /// <summary>
        /// Merge two sorted lists into a single sorted sequence, yielding
        /// elements lazily in ascending order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="a">First sorted list.</param>
        /// <param name="b">Second sorted list.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding elements in sorted order.</returns>
        public static IEnumerable<T> Merge<T>(Sharpy.List<T> a, Sharpy.List<T> b) where T : IComparable<T>
        {
            return MergeInternal(new Sharpy.List<T>[] { a, b });
        }

        /// <summary>
        /// Merge three sorted lists into a single sorted sequence.
        /// </summary>
        public static IEnumerable<T> Merge<T>(Sharpy.List<T> a, Sharpy.List<T> b, Sharpy.List<T> c) where T : IComparable<T>
        {
            return MergeInternal(new Sharpy.List<T>[] { a, b, c });
        }

        /// <summary>
        /// Merge multiple sorted lists into a single sorted sequence.
        /// </summary>
        public static IEnumerable<T> Merge<T>(params Sharpy.List<T>[] iterables) where T : IComparable<T>
        {
            return MergeInternal(iterables);
        }

        /// <summary>
        /// Merge two sorted lists with a key function and/or reverse flag.
        /// Inputs must be pre-sorted according to the key/reverse order.
        /// </summary>
        public static IEnumerable<T> Merge<T, TKey>(Sharpy.List<T> a, Sharpy.List<T> b, Func<T, TKey> key, bool reverse = false) where TKey : IComparable<TKey>
        {
            return MergeInternal(new Sharpy.List<T>[] { a, b }, key, reverse);
        }

        /// <summary>
        /// Merge three sorted lists with a key function and/or reverse flag.
        /// Inputs must be pre-sorted according to the key/reverse order.
        /// </summary>
        public static IEnumerable<T> Merge<T, TKey>(Sharpy.List<T> a, Sharpy.List<T> b, Sharpy.List<T> c, Func<T, TKey> key, bool reverse = false) where TKey : IComparable<TKey>
        {
            return MergeInternal(new Sharpy.List<T>[] { a, b, c }, key, reverse);
        }

        /// <summary>
        /// Merge two sorted lists in reverse order (descending).
        /// Inputs must be pre-sorted in descending order.
        /// </summary>
        public static IEnumerable<T> Merge<T>(Sharpy.List<T> a, Sharpy.List<T> b, bool reverse) where T : IComparable<T>
        {
            Comparison<T> comparison = reverse
                ? (x, y) => y.CompareTo(x)
                : (Comparison<T>)((x, y) => x.CompareTo(y));
            return MergeInternal(new Sharpy.List<T>[] { a, b }, comparison);
        }

        /// <summary>
        /// Merge three sorted lists in reverse order (descending).
        /// Inputs must be pre-sorted in descending order.
        /// </summary>
        public static IEnumerable<T> Merge<T>(Sharpy.List<T> a, Sharpy.List<T> b, Sharpy.List<T> c, bool reverse) where T : IComparable<T>
        {
            Comparison<T> comparison = reverse
                ? (x, y) => y.CompareTo(x)
                : (Comparison<T>)((x, y) => x.CompareTo(y));
            return MergeInternal(new Sharpy.List<T>[] { a, b, c }, comparison);
        }

        private static IEnumerable<T> MergeInternal<T>(Sharpy.List<T>[] iterables) where T : IComparable<T>
        {
            if (iterables == null || iterables.Length == 0)
            {
                yield break;
            }

            // Initialize enumerators and seed with first elements
            var enumerators = new System.Collections.Generic.List<IEnumerator<T>>(iterables.Length);
            // Min-heap of (value, enumeratorIndex)
            var heap = new System.Collections.Generic.List<(T value, int index)>();

            for (int i = 0; i < iterables.Length; i++)
            {
                if (iterables[i] == null)
                {
                    continue;
                }

                var enumerator = ((IEnumerable<T>)iterables[i]).GetEnumerator();
                enumerators.Add(enumerator);
                int enumeratorIndex = enumerators.Count - 1;

                if (enumerator.MoveNext())
                {
                    heap.Add((enumerator.Current, enumeratorIndex));
                }
            }

            // Build initial heap
            for (int i = heap.Count / 2 - 1; i >= 0; i--)
            {
                MergeSiftDown(heap, i);
            }

            try
            {
                while (heap.Count > 0)
                {
                    var (value, idx) = heap[0];
                    yield return value;

                    // Advance the enumerator that produced the min value
                    if (enumerators[idx].MoveNext())
                    {
                        heap[0] = (enumerators[idx].Current, idx);
                        MergeSiftDown(heap, 0);
                    }
                    else
                    {
                        // Remove exhausted enumerator: replace root with last element
                        int lastIdx = heap.Count - 1;
                        if (lastIdx > 0)
                        {
                            heap[0] = heap[lastIdx];
                            heap.RemoveAt(lastIdx);
                            MergeSiftDown(heap, 0);
                        }
                        else
                        {
                            heap.RemoveAt(0);
                        }
                    }
                }
            }
            finally
            {
                // Dispose enumerators even if iteration is abandoned early
                foreach (var enumerator in enumerators)
                {
                    enumerator.Dispose();
                }
            }
        }

        private static void MergeSiftDown<T>(System.Collections.Generic.List<(T value, int index)> heap, int pos) where T : IComparable<T>
        {
            MergeSiftDown(heap, pos, (a, b) => a.CompareTo(b));
        }

        private static void MergeSiftDown<T>(System.Collections.Generic.List<(T value, int index)> heap, int pos, Comparison<T> comparison)
        {
            int count = heap.Count;
            while (true)
            {
                int smallest = pos;
                int left = 2 * pos + 1;
                int right = 2 * pos + 2;

                if (left < count)
                {
                    int cmp = comparison(heap[left].value, heap[smallest].value);
                    if (cmp < 0 || (cmp == 0 && heap[left].index < heap[smallest].index))
                    {
                        smallest = left;
                    }
                }

                if (right < count)
                {
                    int cmp = comparison(heap[right].value, heap[smallest].value);
                    if (cmp < 0 || (cmp == 0 && heap[right].index < heap[smallest].index))
                    {
                        smallest = right;
                    }
                }

                if (smallest == pos)
                {
                    break;
                }

                var temp = heap[pos];
                heap[pos] = heap[smallest];
                heap[smallest] = temp;
                pos = smallest;
            }
        }

        private static IEnumerable<T> MergeInternal<T>(Sharpy.List<T>[] iterables, Comparison<T> comparison)
        {
            if (iterables == null || iterables.Length == 0)
            {
                yield break;
            }

            var enumerators = new System.Collections.Generic.List<IEnumerator<T>>(iterables.Length);
            var heap = new System.Collections.Generic.List<(T value, int index)>();

            for (int i = 0; i < iterables.Length; i++)
            {
                if (iterables[i] == null)
                {
                    continue;
                }

                var enumerator = ((IEnumerable<T>)iterables[i]).GetEnumerator();
                enumerators.Add(enumerator);
                int enumeratorIndex = enumerators.Count - 1;

                if (enumerator.MoveNext())
                {
                    heap.Add((enumerator.Current, enumeratorIndex));
                }
            }

            for (int i = heap.Count / 2 - 1; i >= 0; i--)
            {
                MergeSiftDown(heap, i, comparison);
            }

            try
            {
                while (heap.Count > 0)
                {
                    var (value, idx) = heap[0];
                    yield return value;

                    if (enumerators[idx].MoveNext())
                    {
                        heap[0] = (enumerators[idx].Current, idx);
                        MergeSiftDown(heap, 0, comparison);
                    }
                    else
                    {
                        int lastIdx = heap.Count - 1;
                        if (lastIdx > 0)
                        {
                            heap[0] = heap[lastIdx];
                            heap.RemoveAt(lastIdx);
                            MergeSiftDown(heap, 0, comparison);
                        }
                        else
                        {
                            heap.RemoveAt(0);
                        }
                    }
                }
            }
            finally
            {
                foreach (var enumerator in enumerators)
                {
                    enumerator.Dispose();
                }
            }
        }

        private static IEnumerable<T> MergeInternal<T, TKey>(Sharpy.List<T>[] iterables, Func<T, TKey> key, bool reverse) where TKey : IComparable<TKey>
        {
            Comparison<T> comparison;
            if (reverse)
            {
                comparison = (a, b) => key(b).CompareTo(key(a));
            }
            else
            {
                comparison = (a, b) => key(a).CompareTo(key(b));
            }

            return MergeInternal(iterables, comparison);
        }

        private static void SiftUp<T>(IList<T> heap, int index) where T : IComparable<T>
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (heap[index].CompareTo(heap[parent]) < 0)
                {
                    T temp = heap[index];
                    heap[index] = heap[parent];
                    heap[parent] = temp;
                    index = parent;
                }
                else
                {
                    break;
                }
            }
        }

        private static void SiftDown<T>(IList<T> heap, int index) where T : IComparable<T>
        {
            int count = heap.Count;
            while (true)
            {
                int smallest = index;
                int left = 2 * index + 1;
                int right = 2 * index + 2;

                if (left < count && heap[left].CompareTo(heap[smallest]) < 0)
                {
                    smallest = left;
                }

                if (right < count && heap[right].CompareTo(heap[smallest]) < 0)
                {
                    smallest = right;
                }

                if (smallest == index)
                {
                    break;
                }

                T temp = heap[index];
                heap[index] = heap[smallest];
                heap[smallest] = temp;
                index = smallest;
            }
        }
    }
}
