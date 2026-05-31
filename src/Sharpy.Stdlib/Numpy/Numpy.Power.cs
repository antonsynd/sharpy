using System;

namespace Sharpy
{
    /// <summary>
    /// Provides NumPy-equivalent array power functions.
    /// </summary>
    public static partial class Numpy
    {
        /// <summary>
        /// Elementwise <c>a ** b</c> with broadcasting (NumPy equivalent of <c>numpy.power</c>).
        /// C# has no <c>**</c> operator, so this is exposed as a module function.
        /// </summary>
        /// <param name="a">Base array.</param>
        /// <param name="b">Exponent array. Broadcast against <paramref name="a"/>.</param>
        public static NdArray<double> Power(NdArray<double> a, NdArray<double> b)
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
            int total = ProductOfShapeChecked(shape);

            var data = new double[total];
            var ita = new BroadcastedIterator<double>(a, shape);
            var itb = new BroadcastedIterator<double>(b, shape);

            for (int i = 0; i < total; i++)
            {
                data[i] = System.Math.Pow(ita.Current, itb.Current);
                ita.MoveNext();
                itb.MoveNext();
            }

            return new NdArray<double>(data, shape);
        }

        /// <summary>Raise every element of <paramref name="a"/> to the scalar power <paramref name="b"/>.</summary>
        public static NdArray<double> Power(NdArray<double> a, double b)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var data = new double[a.Size];
            var iter = new BroadcastedIterator<double>(a, a.Shape);
            for (int i = 0; i < a.Size; i++)
            {
                data[i] = System.Math.Pow(iter.Current, b);
                iter.MoveNext();
            }

            return new NdArray<double>(data, a.Shape);
        }

        /// <summary>Raise the scalar <paramref name="a"/> elementwise to the powers in <paramref name="b"/>.</summary>
        public static NdArray<double> Power(double a, NdArray<double> b)
        {
            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            var data = new double[b.Size];
            var iter = new BroadcastedIterator<double>(b, b.Shape);
            for (int i = 0; i < b.Size; i++)
            {
                data[i] = System.Math.Pow(a, iter.Current);
                iter.MoveNext();
            }

            return new NdArray<double>(data, b.Shape);
        }

        private static int ProductOfShapeChecked(int[] shape)
        {
            int size = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                size = checked(size * shape[i]);
            }

            return size;
        }
    }
}
