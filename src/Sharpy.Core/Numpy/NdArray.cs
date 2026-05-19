using System;
using System.Numerics;

namespace Sharpy
{
    /// <summary>
    /// N-dimensional homogeneous array — Sharpy equivalent of <c>numpy.ndarray</c>.
    /// </summary>
    /// <typeparam name="T">Element type. Must be a value type implementing <see cref="IEquatable{T}"/>.</typeparam>
    /// <remarks>
    /// Storage is a flat <c>T[]</c> with a row-major (C-order) stride layout. Views share the
    /// underlying buffer with different shape/strides/offset for zero-copy slicing and reshaping.
    /// </remarks>
    [SharpyModuleType("numpy", "ndarray")]
    public partial class NdArray<T> where T : struct, IEquatable<T>
    {
        internal readonly T[] _data;
        internal readonly int[] _shape;
        internal readonly int[] _strides;
        internal readonly int _offset;

        /// <summary>
        /// Construct an owned <see cref="NdArray{T}"/> backed by <paramref name="data"/> with the given shape.
        /// </summary>
        /// <param name="data">Flat data buffer in row-major order. Length must equal the product of <paramref name="shape"/>.</param>
        /// <param name="shape">Shape of the array. Each dimension must be non-negative.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="shape"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when shape contains negative dimensions or <paramref name="data"/> length does not equal the product of <paramref name="shape"/>.</exception>
        public NdArray(T[] data, int[] shape)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

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

            if (data.Length != size)
            {
                throw new ArgumentException(
                    $"data length {data.Length} does not match shape product {size}",
                    nameof(data));
            }

            _data = data;
            _shape = (int[])shape.Clone();
            _strides = ComputeStrides(_shape);
            _offset = 0;
            Size = size;
        }

        /// <summary>
        /// Internal constructor for views — shares <paramref name="data"/> with explicit
        /// strides and offset. Used by slicing, reshape, and transpose.
        /// </summary>
        internal NdArray(T[] data, int[] shape, int[] strides, int offset)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (strides == null)
            {
                throw new ArgumentNullException(nameof(strides));
            }

            if (strides.Length != shape.Length)
            {
                throw new ArgumentException(
                    $"strides length {strides.Length} does not match shape rank {shape.Length}",
                    nameof(strides));
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

            if (offset < 0 || (size > 0 && offset >= data.Length && data.Length != 0))
            {
                // We allow offset < data.Length for non-zero size; for empty arrays offset can equal data.Length.
                if (offset < 0)
                {
                    throw new ArgumentException($"offset is negative: {offset}", nameof(offset));
                }
            }

            // The buffer must contain at least `offset + Size` elements only for a contiguous default
            // layout; for arbitrary strides we can't validate cheaply. We instead trust internal callers
            // and rely on indexing-time bounds checks for safety.
            _data = data;
            _shape = shape;
            _strides = strides;
            _offset = offset;
            Size = size;
        }

        /// <summary>Number of dimensions (rank) of the array.</summary>
        public int Ndim => _shape.Length;

        /// <summary>Total number of elements (product of shape dimensions).</summary>
        public int Size { get; }

        /// <summary>Shape of the array as a defensive copy of the internal shape vector.</summary>
        public int[] Shape => (int[])_shape.Clone();

        /// <summary>Strides of the array as a defensive copy of the internal stride vector.</summary>
        public int[] Strides => (int[])_strides.Clone();

        /// <summary>Element type name in NumPy-style notation (e.g., <c>float64</c>, <c>int32</c>).</summary>
        public string Dtype => MapDtype(typeof(T));

        /// <summary>
        /// Compute row-major (C-order) strides for the given shape.
        /// </summary>
        internal static int[] ComputeStrides(int[] shape)
        {
            var strides = new int[shape.Length];
            int acc = 1;
            for (int i = shape.Length - 1; i >= 0; i--)
            {
                strides[i] = acc;
                acc = checked(acc * shape[i]);
            }

            return strides;
        }

        private static string MapDtype(Type type)
        {
            if (type == typeof(double))
            {
                return "float64";
            }

            if (type == typeof(float))
            {
                return "float32";
            }

            if (type == typeof(int))
            {
                return "int32";
            }

            if (type == typeof(long))
            {
                return "int64";
            }

            if (type == typeof(short))
            {
                return "int16";
            }

            if (type == typeof(sbyte))
            {
                return "int8";
            }

            if (type == typeof(uint))
            {
                return "uint32";
            }

            if (type == typeof(ulong))
            {
                return "uint64";
            }

            if (type == typeof(ushort))
            {
                return "uint16";
            }

            if (type == typeof(byte))
            {
                return "uint8";
            }

            if (type == typeof(bool))
            {
                return "bool";
            }

            if (type == typeof(Complex))
            {
                return "complex128";
            }

            return type.Name;
        }
    }
}
