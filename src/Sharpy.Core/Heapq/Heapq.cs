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
