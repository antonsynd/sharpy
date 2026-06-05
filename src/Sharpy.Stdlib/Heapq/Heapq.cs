// Generated from src/Sharpy.Stdlib/spy/heapq.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/heapq.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Heap queue algorithm (priority queue).
    /// </summary>
    public static partial class Heapq
    {
        /// <summary>
        /// Push item onto heap, maintaining the heap invariant.
        /// </summary>
        public static void Heappush<T>(Sharpy.List<T> heap, T item)
            where T : global::System.IComparable<T>
        {
            heap.Append(item);
            _SiftUp(heap, global::Sharpy.Builtins.Len(heap) - 1);
        }

        /// <summary>
        /// Pop the smallest item off the heap, maintaining the heap invariant.
        /// </summary>
        public static T Heappop<T>(Sharpy.List<T> heap)
            where T : global::System.IComparable<T>
        {
            if (global::Sharpy.Builtins.Len(heap) == 0)
            {
                throw new global::Sharpy.IndexError("index out of range");
            }

            int lastIdx = global::Sharpy.Builtins.Len(heap) - 1;
            T result = heap[0];
            if (lastIdx > 0)
            {
                heap[0] = heap[lastIdx];
                heap.Pop();
                _SiftDown(heap, 0);
            }
            else
            {
                heap.Pop();
            }

            return result;
        }

        /// <summary>
        /// Transform list into a heap, in-place, in O(len(x)) time.
        /// </summary>
        public static void Heapify<T>(Sharpy.List<T> x)
            where T : global::System.IComparable<T>
        {
            int n = global::Sharpy.Builtins.Len(x);
            int i = (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(n) / 2))) - 1;
            while (i >= 0)
            {
                _SiftDown(x, i);
                i = i - 1;
            }
        }

        /// <summary>
        /// Pop and return the smallest item, and push the new item.
        /// </summary>
        public static T Heapreplace<T>(Sharpy.List<T> heap, T item)
            where T : global::System.IComparable<T>
        {
            if (global::Sharpy.Builtins.Len(heap) == 0)
            {
                throw new global::Sharpy.IndexError("index out of range");
            }

            T result = heap[0];
            heap[0] = item;
            _SiftDown(heap, 0);
            return result;
        }

        /// <summary>
        /// Push item on the heap, then pop and return the smallest item.
        /// </summary>
        public static T Heappushpop<T>(Sharpy.List<T> heap, T item)
            where T : global::System.IComparable<T>
        {
            if (global::Sharpy.Builtins.Len(heap) > 0 && heap[0].CompareTo(item) < 0)
            {
                T result = heap[0];
                heap[0] = item;
                _SiftDown(heap, 0);
                return result;
            }

            return item;
        }

        /// <summary>
        /// Find the n largest elements in a dataset.
        /// </summary>
        public static Sharpy.List<T> Nlargest<T>(int n, Sharpy.List<T> iterable)
            where T : global::System.IComparable<T>
        {
            if (n <= 0)
            {
                return new Sharpy.List<T>()
                {
                };
            }

            Sharpy.List<T> sortedCopy = new global::Sharpy.List<T>(iterable);
            sortedCopy.Sort();
            sortedCopy.Reverse();
            if (n >= global::Sharpy.Builtins.Len(sortedCopy))
            {
                return sortedCopy;
            }

            return global::Sharpy.Slice.GetSlice(sortedCopy, null, n, null);
        }

        /// <summary>
        /// Find the n smallest elements in a dataset.
        /// </summary>
        public static Sharpy.List<T> Nsmallest<T>(int n, Sharpy.List<T> iterable)
            where T : global::System.IComparable<T>
        {
            if (n <= 0)
            {
                return new Sharpy.List<T>()
                {
                };
            }

            Sharpy.List<T> sortedCopy = new global::Sharpy.List<T>(iterable);
            sortedCopy.Sort();
            if (n >= global::Sharpy.Builtins.Len(sortedCopy))
            {
                return sortedCopy;
            }

            return global::Sharpy.Slice.GetSlice(sortedCopy, null, n, null);
        }

        /// <summary>
        /// Merge two sorted inputs into a single sorted output.
        /// </summary>
        public static Sharpy.List<T> Merge<T>(Sharpy.List<T> list1, Sharpy.List<T> list2)
            where T : global::System.IComparable<T>
        {
            Sharpy.List<T> result = new Sharpy.List<T>()
            {
            };
            int i = 0;
            int j = 0;
            while (i < global::Sharpy.Builtins.Len(list1) && j < global::Sharpy.Builtins.Len(list2))
            {
                if (list1[i].CompareTo(list2[j]) <= 0)
                {
                    result.Append(list1[i]);
                    i = i + 1;
                }
                else
                {
                    result.Append(list2[j]);
                    j = j + 1;
                }
            }

            while (i < global::Sharpy.Builtins.Len(list1))
            {
                result.Append(list1[i]);
                i = i + 1;
            }

            while (j < global::Sharpy.Builtins.Len(list2))
            {
                result.Append(list2[j]);
                j = j + 1;
            }

            return result;
        }

        /// <summary>
        /// Merge three sorted inputs into a single sorted output.
        /// </summary>
        public static Sharpy.List<T> Merge<T>(Sharpy.List<T> list1, Sharpy.List<T> list2, Sharpy.List<T> list3)
            where T : global::System.IComparable<T>
        {
            return Merge(Merge(list1, list2), list3);
        }

        /// <summary>
        /// Bubble element at index up to restore the heap invariant.
        /// </summary>
        internal static void _SiftUp<T>(Sharpy.List<T> heap, int index)
            where T : global::System.IComparable<T>
        {
            while (index > 0)
            {
                int parent = (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)((index - 1)) / 2)));
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

        /// <summary>
        /// Push element at index down to restore the heap invariant.
        /// </summary>
        internal static void _SiftDown<T>(Sharpy.List<T> heap, int index)
            where T : global::System.IComparable<T>
        {
            int count = global::Sharpy.Builtins.Len(heap);
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
