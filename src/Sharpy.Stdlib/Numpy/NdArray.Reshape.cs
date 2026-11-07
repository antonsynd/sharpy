using System;

namespace Sharpy
{
    /// <summary>N-dimensional array with NumPy-compatible semantics.</summary>
    public partial class NdArray<T>
    {
        /// <summary>
        /// Return an array with the same data and a new shape. Returns a zero-copy view when
        /// this array is C-contiguous; otherwise materializes a copy.
        /// </summary>
        /// <param name="newShape">
        /// The target shape. Exactly one dimension may be <c>-1</c>, in which case its size is
        /// inferred from the total element count and the remaining dimensions.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="newShape"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when more than one dimension is -1, or the inferred shape does not match <see cref="Size"/>.</exception>
        public NdArray<T> Reshape(params int[] newShape)
        {
            if (newShape == null)
            {
                throw new ArgumentNullException(nameof(newShape));
            }

            int[] resolved = ResolveShape(newShape, Size);

            if (IsContiguous())
            {
                var strides = ComputeStrides(resolved);
                return new NdArray<T>(_data, resolved, strides, _offset);
            }

            // Non-contiguous — materialize a copy in row-major order.
            var copy = new T[Size];
            CopyToFlat(copy);
            return new NdArray<T>(copy, resolved);
        }

        /// <summary>
        /// Return a view of this array with axes reversed. For a 2-D array this is the matrix transpose.
        /// </summary>
        public NdArray<T> Transpose()
        {
            int n = _shape.Length;
            var newShape = new int[n];
            var newStrides = new int[n];
            for (int i = 0; i < n; i++)
            {
                newShape[i] = _shape[n - 1 - i];
                newStrides[i] = _strides[n - 1 - i];
            }

            return new NdArray<T>(_data, newShape, newStrides, _offset);
        }

        // NOTE: NumPy exposes the transpose as a `.T` property. Because our type parameter is
        // also named `T`, we expose the same operation as the `Transpose()` method only.
        // TODO(#646): Rename the type parameter to expose a `.T` property at the C# surface.

        /// <summary>
        /// Return a 1-D copy of this array's elements in row-major order.
        /// </summary>
        public NdArray<T> Flatten()
        {
            var copy = new T[Size];
            CopyToFlat(copy);
            return new NdArray<T>(copy, new[] { Size });
        }

        /// <summary>
        /// Return a 1-D view of this array if it is C-contiguous; otherwise return a 1-D copy.
        /// </summary>
        public NdArray<T> Ravel()
        {
            if (IsContiguous())
            {
                var strides = new[] { 1 };
                return new NdArray<T>(_data, new[] { Size }, strides, _offset);
            }

            return Flatten();
        }

        /// <summary>
        /// Return a deep copy of this array. The result owns its buffer and is C-contiguous.
        /// </summary>
        public NdArray<T> Copy()
        {
            var copy = new T[Size];
            CopyToFlat(copy);
            return new NdArray<T>(copy, (int[])_shape.Clone());
        }

        /// <summary>
        /// True when this array is C-contiguous: strides match the standard row-major layout
        /// and the data starts at offset 0.
        /// </summary>
        internal bool IsContiguous()
        {
            if (_offset != 0)
            {
                return false;
            }

            int[] expected = ComputeStrides(_shape);
            for (int i = 0; i < _strides.Length; i++)
            {
                if (_shape[i] == 0)
                {
                    continue;
                }

                if (_shape[i] == 1)
                {
                    // Stride is irrelevant for size-1 axes.
                    continue;
                }

                if (_strides[i] != expected[i])
                {
                    return false;
                }
            }

            return _data.Length == Size;
        }

        /// <summary>
        /// Copy elements of this (possibly non-contiguous) array to a flat row-major buffer.
        /// </summary>
        internal void CopyToFlat(T[] dest)
        {
            if (dest == null)
            {
                throw new ArgumentNullException(nameof(dest));
            }

            if (Size == 0)
            {
                return;
            }

            int ndim = _shape.Length;
            var idx = new int[ndim];
            for (int flat = 0; flat < Size; flat++)
            {
                int srcOffset = _offset;
                for (int axis = 0; axis < ndim; axis++)
                {
                    srcOffset += idx[axis] * _strides[axis];
                }

                dest[flat] = _data[srcOffset];

                // Increment N-D index in row-major order.
                for (int axis = ndim - 1; axis >= 0; axis--)
                {
                    idx[axis]++;
                    if (idx[axis] < _shape[axis])
                    {
                        break;
                    }

                    idx[axis] = 0;
                }
            }
        }

        private static int[] ResolveShape(int[] shape, int totalSize)
        {
            int inferAxis = -1;
            int known = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i] == -1)
                {
                    if (inferAxis != -1)
                    {
                        throw new ValueError("can only specify one unknown dimension");
                    }

                    inferAxis = i;
                }
                else if (shape[i] < 0)
                {
                    throw new ValueError("negative dimensions not allowed");
                }
                else
                {
                    known = checked(known * shape[i]);
                }
            }

            var resolved = new int[shape.Length];
            System.Array.Copy(shape, resolved, shape.Length);

            if (inferAxis != -1)
            {
                if (known == 0)
                {
                    throw new ValueError(CannotReshape(totalSize, shape));
                }

                if (totalSize % known != 0)
                {
                    throw new ValueError(CannotReshape(totalSize, shape));
                }

                resolved[inferAxis] = totalSize / known;
            }
            else if (known != totalSize)
            {
                throw new ValueError(CannotReshape(totalSize, shape));
            }

            return resolved;
        }

        // numpy's text: "cannot reshape array of size 6 into shape (2,newaxis,4)" — the unknown
        // dimension spelled "newaxis" (#2461).
        private static string CannotReshape(int totalSize, int[] shape)
        {
            var parts = new string[shape.Length];
            for (int i = 0; i < shape.Length; i++)
            {
                parts[i] = shape[i] == -1 ? "newaxis" : shape[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            string spelled = shape.Length == 1 ? "(" + parts[0] + ",)" : "(" + string.Join(",", parts) + ")";
            return "cannot reshape array of size " + totalSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + " into shape " + spelled;
        }
    }
}
