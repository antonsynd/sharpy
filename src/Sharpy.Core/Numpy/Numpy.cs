using System;

namespace Sharpy
{
    /// <summary>
    /// NumPy-equivalent module — provides <see cref="NdArray{T}"/> array creation and
    /// elementwise primitives. Mirrors the surface area of the Python <c>numpy</c> package.
    /// </summary>
    public static partial class Numpy
    {
        /// <summary>
        /// Construct a 1-D <see cref="NdArray{T}"/> from a flat data buffer.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="data">Source data. Length determines the shape.</param>
        /// <returns>A new 1-D ndarray owning a copy of <paramref name="data"/>.</returns>
        public static NdArray<T> Array<T>(System.Collections.Generic.IEnumerable<T> data)
            where T : struct, IEquatable<T>
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Fast path for IList<T> / ICollection<T> to avoid the LINQ overhead.
            if (data is System.Collections.Generic.IList<T> list)
            {
                var copy = new T[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    copy[i] = list[i];
                }

                return new NdArray<T>(copy, new[] { list.Count });
            }

            var buffer = new System.Collections.Generic.List<T>();
            foreach (var item in data)
            {
                buffer.Add(item);
            }

            var result = new T[buffer.Count];
            for (int i = 0; i < buffer.Count; i++)
            {
                result[i] = buffer[i];
            }

            return new NdArray<T>(result, new[] { buffer.Count });
        }

        /// <summary>
        /// Return a new ndarray of the given shape, filled with 0.0.
        /// </summary>
        /// <param name="shape">Shape of the result. Each dimension must be non-negative.</param>
        public static NdArray<double> Zeros(params int[] shape)
        {
            int size = ProductOfShape(shape);
            var data = new double[size];
            return new NdArray<double>(data, shape);
        }

        /// <summary>
        /// Return a new ndarray of the given shape, filled with 1.0.
        /// </summary>
        /// <param name="shape">Shape of the result. Each dimension must be non-negative.</param>
        public static NdArray<double> Ones(params int[] shape)
        {
            int size = ProductOfShape(shape);
            var data = new double[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = 1.0;
            }

            return new NdArray<double>(data, shape);
        }

        /// <summary>
        /// Return a new ndarray of the given shape, filled with <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="shape">Shape of the result.</param>
        /// <param name="value">Fill value.</param>
        public static NdArray<T> Full<T>(int[] shape, T value) where T : struct, IEquatable<T>
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            int size = ProductOfShape(shape);
            var data = new T[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = value;
            }

            return new NdArray<T>(data, shape);
        }

        /// <summary>
        /// Return an <paramref name="n"/>×<paramref name="n"/> identity matrix.
        /// </summary>
        /// <param name="n">Square matrix dimension.</param>
        public static NdArray<double> Eye(int n)
        {
            if (n < 0)
            {
                throw new ArgumentException("n must be non-negative", nameof(n));
            }

            var data = new double[n * n];
            for (int i = 0; i < n; i++)
            {
                data[i * n + i] = 1.0;
            }

            return new NdArray<double>(data, new[] { n, n });
        }

        /// <summary>
        /// Return evenly spaced values within a half-open interval <c>[start, stop)</c>.
        /// </summary>
        /// <param name="start">Inclusive start of the interval.</param>
        /// <param name="stop">Exclusive end of the interval.</param>
        /// <param name="step">Step size between successive values. Default 1.0. Cannot be zero.</param>
        public static NdArray<double> Arange(double start, double stop, double step = 1.0)
        {
            if (step == 0)
            {
                throw new ArgumentException("step cannot be zero", nameof(step));
            }

            int count;
            if (step > 0)
            {
                count = stop > start ? (int)System.Math.Ceiling((stop - start) / step) : 0;
            }
            else
            {
                count = stop < start ? (int)System.Math.Ceiling((stop - start) / step) : 0;
            }

            if (count < 0)
            {
                count = 0;
            }

            var data = new double[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = start + i * step;
            }

            return new NdArray<double>(data, new[] { count });
        }

        /// <summary>
        /// Return <paramref name="num"/> evenly spaced samples over the closed interval <c>[start, stop]</c>.
        /// </summary>
        /// <param name="start">Inclusive start of the interval.</param>
        /// <param name="stop">Inclusive end of the interval.</param>
        /// <param name="num">Number of samples to generate. Must be non-negative. Default 50.</param>
        public static NdArray<double> Linspace(double start, double stop, int num = 50)
        {
            if (num < 0)
            {
                throw new ArgumentException("num must be non-negative", nameof(num));
            }

            var data = new double[num];
            if (num == 0)
            {
                return new NdArray<double>(data, new[] { 0 });
            }

            if (num == 1)
            {
                data[0] = start;
                return new NdArray<double>(data, new[] { 1 });
            }

            double step = (stop - start) / (num - 1);
            for (int i = 0; i < num; i++)
            {
                data[i] = start + i * step;
            }

            // Ensure the last sample is exactly stop (matches numpy behavior).
            data[num - 1] = stop;

            return new NdArray<double>(data, new[] { num });
        }

        /// <summary>
        /// Return a new uninitialized ndarray of the given shape. Backed by a fresh
        /// zero-initialized buffer (CLR semantics — no truly-uninitialized storage).
        /// </summary>
        /// <param name="shape">Shape of the result.</param>
        public static NdArray<double> Empty(params int[] shape)
        {
            int size = ProductOfShape(shape);
            var data = new double[size];
            return new NdArray<double>(data, shape);
        }

        /// <summary>
        /// Dot product of two arrays — top-level alias for <see cref="NumpyLinalg.Dot"/>.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static NdArray<double> Dot(NdArray<double> a, NdArray<double> b) => NumpyLinalg.Dot(a, b);

        /// <summary>
        /// Matrix product — top-level alias for <see cref="NumpyLinalg.Matmul"/>.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static NdArray<double> Matmul(NdArray<double> a, NdArray<double> b) => NumpyLinalg.Matmul(a, b);

        private static int ProductOfShape(int[] shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            int size = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i] < 0)
                {
                    throw new ArgumentException($"shape dimension {i} is negative: {shape[i]}", nameof(shape));
                }

                size = checked(size * shape[i]);
            }

            return size;
        }
    }
}
