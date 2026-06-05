// Merge functions — kept in C# due to yield return, Func<T,TKey>, params, and tuple requirements.
using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Heap queue algorithm (priority queue).</summary>
    public static partial class Heapq
    {
        /// <summary>Merge multiple sorted inputs into a single sorted output.</summary>
        public static IEnumerable<T> Merge<T>(params Sharpy.List<T>[] iterables) where T : IComparable<T>
        {
            return MergeInternal(iterables);
        }

        /// <summary>Merge two sorted inputs into a single sorted output, using a key function.</summary>
        public static IEnumerable<T> Merge<T, TKey>(Sharpy.List<T> a, Sharpy.List<T> b, Func<T, TKey> key, bool reverse = false) where TKey : IComparable<TKey>
        {
            return MergeInternal(new Sharpy.List<T>[] { a, b }, key, reverse);
        }

        /// <summary>Merge three sorted inputs into a single sorted output, using a key function.</summary>
        public static IEnumerable<T> Merge<T, TKey>(Sharpy.List<T> a, Sharpy.List<T> b, Sharpy.List<T> c, Func<T, TKey> key, bool reverse = false) where TKey : IComparable<TKey>
        {
            return MergeInternal(new Sharpy.List<T>[] { a, b, c }, key, reverse);
        }

        /// <summary>Merge multiple sorted inputs into a single sorted output, with optional reverse ordering.</summary>
        public static IEnumerable<T> Merge<T>(Sharpy.List<T>[] iterables, bool reverse = false) where T : IComparable<T>
        {
            Comparison<T> comparison = reverse
                ? (x, y) => y.CompareTo(x)
                : (Comparison<T>)((x, y) => x.CompareTo(y));
            return MergeInternal(iterables, comparison);
        }

        /// <summary>Merge multiple sorted inputs into a single sorted output, using a key function.</summary>
        public static IEnumerable<T> Merge<T, TKey>(Sharpy.List<T>[] iterables, Func<T, TKey> key, bool reverse = false) where TKey : IComparable<TKey>
        {
            return MergeInternal(iterables, key, reverse);
        }

        /// <summary>Merge two sorted inputs into a single sorted output, with optional reverse ordering.</summary>
        public static IEnumerable<T> Merge<T>(Sharpy.List<T> a, Sharpy.List<T> b, bool reverse) where T : IComparable<T>
        {
            Comparison<T> comparison = reverse
                ? (x, y) => y.CompareTo(x)
                : (Comparison<T>)((x, y) => x.CompareTo(y));
            return MergeInternal(new Sharpy.List<T>[] { a, b }, comparison);
        }

        /// <summary>Merge three sorted inputs into a single sorted output, with optional reverse ordering.</summary>
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
                MergeSiftDown(heap, i);
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
                        MergeSiftDown(heap, 0);
                    }
                    else
                    {
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
    }
}
