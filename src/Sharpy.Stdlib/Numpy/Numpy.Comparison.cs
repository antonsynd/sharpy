using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Provides NumPy-equivalent array comparison functions.
    /// </summary>
    public static partial class Numpy
    {
        /// <summary>
        /// Elementwise <c>a == b</c> with broadcasting, returning a boolean ndarray.
        /// </summary>
        public static NdArray<bool> Equal<T>(NdArray<T> a, NdArray<T> b)
            where T : struct, IEquatable<T> =>
            CompareElementwise(a, b, CompareKind.Equal);

        /// <summary>Elementwise <c>a != b</c> with broadcasting.</summary>
        public static NdArray<bool> NotEqual<T>(NdArray<T> a, NdArray<T> b)
            where T : struct, IEquatable<T> =>
            CompareElementwise(a, b, CompareKind.NotEqual);

        /// <summary>Elementwise <c>a &lt; b</c> with broadcasting.</summary>
        public static NdArray<bool> Less<T>(NdArray<T> a, NdArray<T> b)
            where T : struct, IEquatable<T> =>
            CompareElementwise(a, b, CompareKind.Less);

        /// <summary>Elementwise <c>a &lt;= b</c> with broadcasting.</summary>
        public static NdArray<bool> LessEqual<T>(NdArray<T> a, NdArray<T> b)
            where T : struct, IEquatable<T> =>
            CompareElementwise(a, b, CompareKind.LessEqual);

        /// <summary>Elementwise <c>a &gt; b</c> with broadcasting.</summary>
        public static NdArray<bool> Greater<T>(NdArray<T> a, NdArray<T> b)
            where T : struct, IEquatable<T> =>
            CompareElementwise(a, b, CompareKind.Greater);

        /// <summary>Elementwise <c>a &gt;= b</c> with broadcasting.</summary>
        public static NdArray<bool> GreaterEqual<T>(NdArray<T> a, NdArray<T> b)
            where T : struct, IEquatable<T> =>
            CompareElementwise(a, b, CompareKind.GreaterEqual);

        private enum CompareKind
        {
            Equal,
            NotEqual,
            Less,
            LessEqual,
            Greater,
            GreaterEqual
        }

        private static NdArray<bool> CompareElementwise<T>(NdArray<T> a, NdArray<T> b, CompareKind kind)
            where T : struct, IEquatable<T>
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

            var data = new bool[total];
            var ita = new BroadcastedIterator<T>(a, shape);
            var itb = new BroadcastedIterator<T>(b, shape);

            // Equality-only comparisons rely on IEquatable<T>; ordering comparisons need Comparer<T>.
            if (kind == CompareKind.Equal)
            {
                for (int i = 0; i < total; i++)
                {
                    data[i] = ita.Current.Equals(itb.Current);
                    ita.MoveNext();
                    itb.MoveNext();
                }
            }
            else if (kind == CompareKind.NotEqual)
            {
                for (int i = 0; i < total; i++)
                {
                    data[i] = !ita.Current.Equals(itb.Current);
                    ita.MoveNext();
                    itb.MoveNext();
                }
            }
            else
            {
                var cmp = Comparer<T>.Default;
                for (int i = 0; i < total; i++)
                {
                    int c = cmp.Compare(ita.Current, itb.Current);
                    data[i] = kind switch
                    {
                        CompareKind.Less => c < 0,
                        CompareKind.LessEqual => c <= 0,
                        CompareKind.Greater => c > 0,
                        CompareKind.GreaterEqual => c >= 0,
                        _ => throw new InvalidOperationException(),
                    };
                    ita.MoveNext();
                    itb.MoveNext();
                }
            }

            return new NdArray<bool>(data, shape);
        }
    }
}
