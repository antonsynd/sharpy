using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Provides NumPy-equivalent array utility functions (sort, search, unique, allclose, etc.).
    /// </summary>
    public static partial class Numpy
    {
        /// <summary>
        /// Return a sorted copy of the input. For 1-D input this is a plain ascending sort;
        /// for higher-rank inputs the array is flattened first.
        /// </summary>
        public static NdArray<double> Sort(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var flat = new double[a.Size];
            a.CopyToFlat(flat);
            System.Array.Sort(flat);
            return new NdArray<double>(flat, new[] { a.Size });
        }

        /// <summary>
        /// Return the indices that would sort the input — i.e. <c>a.Sort()</c> is equivalent
        /// to <c>a.Take(Argsort(a))</c> for 1-D inputs.
        /// </summary>
        public static NdArray<long> Argsort(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var flat = new double[a.Size];
            a.CopyToFlat(flat);

            var indices = new int[a.Size];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            System.Array.Sort(indices, (x, y) => flat[x].CompareTo(flat[y]));

            var result = new long[a.Size];
            for (int i = 0; i < indices.Length; i++)
            {
                result[i] = indices[i];
            }

            return new NdArray<long>(result, new[] { a.Size });
        }

        /// <summary>
        /// Return the sorted unique elements of <paramref name="a"/> as a 1-D array.
        /// </summary>
        public static NdArray<double> Unique(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var flat = new double[a.Size];
            a.CopyToFlat(flat);
            System.Array.Sort(flat);

            // Compact in-place using strict-greater since sorted; NaN sorts to the end.
            int writeIdx = 0;
            for (int i = 0; i < flat.Length; i++)
            {
                if (i == 0 || !EqualOrBothNaN(flat[i], flat[writeIdx - 1]))
                {
                    flat[writeIdx++] = flat[i];
                }
            }

            var result = new double[writeIdx];
            System.Array.Copy(flat, result, writeIdx);
            return new NdArray<double>(result, new[] { writeIdx });
        }

        /// <summary>
        /// Find indices where elements of <paramref name="values"/> should be inserted into
        /// the sorted 1-D array <paramref name="a"/> to maintain order. Uses NumPy's "left" side
        /// convention (the first valid insertion point).
        /// </summary>
        public static NdArray<long> Searchsorted(NdArray<double> a, NdArray<double> values)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (a.Ndim != 1)
            {
                throw new ArgumentException($"a must be 1-D, got {a.Ndim}-D", nameof(a));
            }

            var aFlat = new double[a.Size];
            a.CopyToFlat(aFlat);

            var vFlat = new double[values.Size];
            values.CopyToFlat(vFlat);

            var result = new long[values.Size];
            for (int i = 0; i < vFlat.Length; i++)
            {
                result[i] = SearchLeft(aFlat, vFlat[i]);
            }

            return new NdArray<long>(result, values.Shape);
        }

        /// <summary>
        /// True if every pair of elements in <paramref name="a"/> and <paramref name="b"/> is
        /// close, using NumPy's mixed absolute/relative tolerance:
        /// <c>|a - b| &lt;= atol + rtol * |b|</c>.
        /// </summary>
        public static bool Allclose(NdArray<double> a, NdArray<double> b, double rtol = 1e-5, double atol = 1e-8)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            int[] shape = Broadcasting.BroadcastShapes(a.Shape, b.Shape);
            int total = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                total = checked(total * shape[i]);
            }

            var ita = new BroadcastedIterator<double>(a, shape);
            var itb = new BroadcastedIterator<double>(b, shape);

            for (int i = 0; i < total; i++)
            {
                double x = ita.Current;
                double y = itb.Current;
                if (!IsClose(x, y, rtol, atol))
                {
                    return false;
                }

                ita.MoveNext();
                itb.MoveNext();
            }

            return true;
        }

        /// <summary>Elementwise <c>double.IsNaN</c>.</summary>
        public static NdArray<bool> Isnan(NdArray<double> a) => MapBool(a, double.IsNaN);

        /// <summary>Elementwise <c>double.IsInfinity</c>.</summary>
        public static NdArray<bool> Isinf(NdArray<double> a) => MapBool(a, double.IsInfinity);

        /// <summary>Elementwise <c>double.IsFinite</c> (neither infinite nor NaN).</summary>
        public static NdArray<bool> Isfinite(NdArray<double> a) => MapBool(a, x => !double.IsNaN(x) && !double.IsInfinity(x));

        // -- Internal helpers -------------------------------------------------------

        private static NdArray<bool> MapBool(NdArray<double> a, Func<double, bool> fn)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var data = new bool[a.Size];
            var iter = new BroadcastedIterator<double>(a, a.Shape);
            for (int i = 0; i < a.Size; i++)
            {
                data[i] = fn(iter.Current);
                iter.MoveNext();
            }

            return new NdArray<bool>(data, a.Shape);
        }

        private static bool EqualOrBothNaN(double x, double y)
        {
            if (double.IsNaN(x) && double.IsNaN(y))
            {
                return true;
            }

            return x == y;
        }

        private static bool IsClose(double x, double y, double rtol, double atol)
        {
            if (double.IsNaN(x) || double.IsNaN(y))
            {
                return double.IsNaN(x) && double.IsNaN(y);
            }

            if (double.IsInfinity(x) || double.IsInfinity(y))
            {
                return x == y;
            }

            return System.Math.Abs(x - y) <= atol + rtol * System.Math.Abs(y);
        }

        // Standard binary search returning the leftmost insertion index.
        private static int SearchLeft(double[] sorted, double value)
        {
            int lo = 0;
            int hi = sorted.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sorted[mid] < value)
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
    }
}
