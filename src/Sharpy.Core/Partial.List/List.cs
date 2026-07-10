using System.Collections.Generic;
using System;
namespace Sharpy
{
    /// <summary>
    /// A mutable sequence of elements, similar to Python's <c>list</c>.
    /// Supports negative indexing, slicing, and Python-style methods.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list</typeparam>
    public sealed partial class List<T>
        : IList<T>,
          IReadOnlyList<T>,
          System.Collections.IList,
          System.IEquatable<List<T>>,
          ISized,
          IDeepCopyable,
          IShallowCopyable
    {
        // Internal for direct backing access by the list enumerators.
        internal System.Collections.Generic.List<T> _list;

        /// <summary>
        /// Constructs an empty list.
        /// </summary>
        public List()
        {
            _list = new System.Collections.Generic.List<T>();
        }

        /// <summary>
        /// Constructs a list with a shallow copy of the elements in the
        /// iterable.
        /// </summary>
        public List(IEnumerable<T> enumerable) : this()
        {
            if (enumerable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            // Presize when the element count is known up front so the copy
            // does not reallocate as it grows.
            if (enumerable is ICollection<T> collection)
            {
                _list.Capacity = collection.Count;
            }

            _list.AddRange(enumerable);
        }

        /// <summary>
        /// Implicitly converts a .NET array to a Sharpy list.
        /// </summary>
        public static implicit operator List<T>(T[] array)
        {
            return new List<T>(array);
        }

        /// <remarks>
        /// For collection initializers. Also a part of the
        /// System.Collections.Generic.ICollection interface.
        /// </remarks>
        public void Add(T item) => _list.Add(item);

        /// <summary>
        /// Return a shallow copy of the list.
        /// </summary>
        /// <returns>A new list with the same elements.</returns>
        /// <example>
        /// <code>
        /// x = [1, 2, 3]
        /// y = x.copy()    # [1, 2, 3]
        /// </code>
        /// </example>
        public List<T> Copy()
        {
            var newList = new List<T>();
            newList._list.AddRange(_list);

            return newList;
        }

        /// <inheritdoc/>
        object IShallowCopyable.ShallowCopy()
        {
            return Copy();
        }

        /// <inheritdoc/>
        object IDeepCopyable.DeepCopy(Dictionary<object, object> memo)
        {
            var newList = new List<T>();
            memo[this] = newList;

            foreach (var item in _list)
            {
                object? copiedItem = item != null
                    ? CopyModule.DeepCopyInternal(item, memo)
                    : default(T);
                newList._list.Add((T)copiedItem!);
            }

            return newList;
        }

        /// <summary>
        /// Sort the items of the list in place (the arguments can be used for
        /// sort customization, see Sorted() for their explanation).
        /// </summary>
        /// <param name="reverse">If <c>true</c>, sort in descending order.</param>
        /// <remarks>
        /// Matches Python's stable sort for reference/nullable element types
        /// (the relative order of comparer-equal elements is preserved, and
        /// <paramref name="reverse"/> preserves the original order among equal
        /// elements rather than reversing it). For value-type elements, where
        /// two comparer-equal elements are indistinguishable, the faster
        /// in-place sort is used.
        /// </remarks>
        /// <example>
        /// <code>
        /// x = [3, 1, 2]
        /// x.sort()             # [1, 2, 3]
        /// x.sort(reverse=True) # [3, 2, 1]
        /// </code>
        /// </example>
        public void Sort(bool reverse = false)
        {
            if (_list.Count < 2)
            {
                return;
            }

            if (default(T) is null)
            {
                // Reference or nullable element type: the relative order of
                // distinct-but-equal-comparing elements is observable, so keep
                // Python's stable ordering. ComparerAdapter reproduces the
                // keyless comparison semantics (throwing when comparing against
                // None) without allocating an identity key selector per compare.
                StableSort(ComparerAdapter<T>.Instance, reverse);
            }
            else
            {
                // Value-type element: comparer-equal elements are
                // indistinguishable, so stability is unobservable and None
                // cannot occur. Use the in-place sort with the runtime's
                // specialized default comparer (no boxing, no delegate hop).
                _list.Sort();

                if (reverse)
                {
                    _list.Reverse();
                }
            }
        }

        /// <summary>
        /// Stable bottom-up merge sort over the backing list. When
        /// <paramref name="reverse"/> is set, sorts in descending order while
        /// preserving the original order among comparer-equal elements
        /// (matching Python's <c>reverse=True</c> — not a sort-then-reverse).
        /// </summary>
        private void StableSort(IComparer<T> comparer, bool reverse)
        {
            int n = _list.Count;
            var src = new T[n];
            _list.CopyTo(src);
            var dst = new T[n];

            for (int width = 1; width < n; width <<= 1)
            {
                for (int i = 0; i < n; i += width << 1)
                {
                    int left = i;
                    int mid = System.Math.Min(i + width, n);
                    int right = System.Math.Min(i + (width << 1), n);
                    MergeRuns(src, dst, left, mid, right, comparer, reverse);
                }

                var swap = src;
                src = dst;
                dst = swap;
            }

            for (int i = 0; i < n; i++)
            {
                _list[i] = src[i];
            }
        }

        private static void MergeRuns(
            T[] src, T[] dst, int left, int mid, int right, IComparer<T> comparer, bool reverse)
        {
            int i = left;
            int j = mid;
            int k = left;

            while (i < mid && j < right)
            {
                int c = comparer.Compare(src[i], src[j]);
                // On ties (c == 0) always take the left run first so equal
                // elements keep their original relative order in both
                // directions (stable ascending and stable descending).
                bool takeLeft = reverse ? c >= 0 : c <= 0;

                if (takeLeft)
                {
                    dst[k++] = src[i++];
                }
                else
                {
                    dst[k++] = src[j++];
                }
            }

            while (i < mid)
            {
                dst[k++] = src[i++];
            }

            while (j < right)
            {
                dst[k++] = src[j++];
            }
        }

        /// <summary>
        /// Sort the items of the list in place (the arguments can be used for
        /// sort customization, see Sorted() for their explanation).
        /// </summary>
        /// <remarks>
        /// This is not a stable sort.
        /// </remarks>
        public void Sort<TKey>(Func<T, TKey> key, bool reverse = false)
        {
            if (key is null)
            {
                throw TypeError.ArgNone("sort", "key");
            }

            var comp = KeyComparerFactory<T, TKey>.Create(key);

            if (reverse)
            {
                _list.Sort(Comparer<T>.Create((a, b) => comp.Compare(b, a)));
            }
            else
            {
                _list.Sort(comp);
            }
        }

        /// <summary>
        /// Creates a shallow copy this list as a .NET list.
        /// </summary>
        public System.Collections.Generic.List<T> ToList()
        {
            return new System.Collections.Generic.List<T>(_list);
        }
    }
}
