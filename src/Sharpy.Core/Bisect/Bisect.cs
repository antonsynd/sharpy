using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Array bisection algorithm, similar to Python's bisect module.
    /// Provides functions to maintain a list in sorted order without
    /// having to sort the list after each insertion.
    /// </summary>
    public static partial class Bisect
    {
        /// <summary>
        /// Locate the leftmost insertion point for <paramref name="x"/> in
        /// <paramref name="a"/> to maintain sorted order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="a">A sorted list to search.</param>
        /// <param name="x">The value to locate an insertion point for.</param>
        /// <param name="lo">The lower bound of the slice to search (inclusive).</param>
        /// <param name="hi">The upper bound of the slice to search (exclusive). -1 means len(a).</param>
        /// <returns>The leftmost index where <paramref name="x"/> can be inserted.</returns>
        /// <example>
        /// <code>
        /// bisect.bisect_left([1, 2, 3, 4, 5], 3)    # 2
        /// bisect.bisect_left([1, 1, 1], 1)           # 0
        /// </code>
        /// </example>
        public static int BisectLeft<T>(IList<T> a, T x, int lo = 0, int hi = -1) where T : IComparable<T>
        {
            if (hi == -1)
            {
                hi = a.Count;
            }

            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (a[mid].CompareTo(x) < 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }

        /// <summary>
        /// Locate the rightmost insertion point for <paramref name="x"/> in
        /// <paramref name="a"/> to maintain sorted order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="a">A sorted list to search.</param>
        /// <param name="x">The value to locate an insertion point for.</param>
        /// <param name="lo">The lower bound of the slice to search (inclusive).</param>
        /// <param name="hi">The upper bound of the slice to search (exclusive). -1 means len(a).</param>
        /// <returns>The rightmost index where <paramref name="x"/> can be inserted.</returns>
        /// <example>
        /// <code>
        /// bisect.bisect_right([1, 2, 3, 4, 5], 3)   # 3
        /// bisect.bisect_right([1, 1, 1], 1)          # 3
        /// </code>
        /// </example>
        public static int BisectRight<T>(IList<T> a, T x, int lo = 0, int hi = -1) where T : IComparable<T>
        {
            if (hi == -1)
            {
                hi = a.Count;
            }

            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (a[mid].CompareTo(x) > 0)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            return lo;
        }

        /// <summary>
        /// Alias for <see cref="BisectRight{T}"/>. Locate the rightmost insertion point
        /// for <paramref name="x"/> in <paramref name="a"/> to maintain sorted order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="a">A sorted list to search.</param>
        /// <param name="x">The value to locate an insertion point for.</param>
        /// <param name="lo">The lower bound of the slice to search (inclusive).</param>
        /// <param name="hi">The upper bound of the slice to search (exclusive). -1 means len(a).</param>
        /// <returns>The rightmost index where <paramref name="x"/> can be inserted.</returns>
        public static int BisectSearch<T>(IList<T> a, T x, int lo = 0, int hi = -1) where T : IComparable<T>
        {
            return BisectRight(a, x, lo, hi);
        }

        /// <summary>
        /// Insert <paramref name="x"/> in <paramref name="a"/> in sorted order,
        /// inserting at the leftmost suitable position.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="a">A sorted list to insert into.</param>
        /// <param name="x">The value to insert.</param>
        /// <param name="lo">The lower bound of the slice to search (inclusive).</param>
        /// <param name="hi">The upper bound of the slice to search (exclusive). -1 means len(a).</param>
        /// <example>
        /// <code>
        /// a = [1, 3, 5]
        /// bisect.insort_left(a, 3)    # a is now [1, 3, 3, 5]
        /// </code>
        /// </example>
        public static void InsortLeft<T>(IList<T> a, T x, int lo = 0, int hi = -1) where T : IComparable<T>
        {
            int idx = BisectLeft(a, x, lo, hi);
            a.Insert(idx, x);
        }

        /// <summary>
        /// Insert <paramref name="x"/> in <paramref name="a"/> in sorted order,
        /// inserting at the rightmost suitable position.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="a">A sorted list to insert into.</param>
        /// <param name="x">The value to insert.</param>
        /// <param name="lo">The lower bound of the slice to search (inclusive).</param>
        /// <param name="hi">The upper bound of the slice to search (exclusive). -1 means len(a).</param>
        /// <example>
        /// <code>
        /// a = [1, 3, 5]
        /// bisect.insort_right(a, 4)    # a is now [1, 3, 4, 5]
        /// </code>
        /// </example>
        public static void InsortRight<T>(IList<T> a, T x, int lo = 0, int hi = -1) where T : IComparable<T>
        {
            int idx = BisectRight(a, x, lo, hi);
            a.Insert(idx, x);
        }

        /// <summary>
        /// Alias for <see cref="InsortRight{T}"/>. Insert <paramref name="x"/> in
        /// <paramref name="a"/> in sorted order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="a">A sorted list to insert into.</param>
        /// <param name="x">The value to insert.</param>
        /// <param name="lo">The lower bound of the slice to search (inclusive).</param>
        /// <param name="hi">The upper bound of the slice to search (exclusive). -1 means len(a).</param>
        public static void Insort<T>(IList<T> a, T x, int lo = 0, int hi = -1) where T : IComparable<T>
        {
            InsortRight(a, x, lo, hi);
        }
    }
}
