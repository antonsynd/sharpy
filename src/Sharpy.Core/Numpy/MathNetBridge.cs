using System;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Sharpy
{
    /// <summary>
    /// Bridge utilities between Sharpy's <see cref="NdArray{T}"/> and Math.NET Numerics' matrix/vector types.
    /// Used by <see cref="NumpyLinalg"/> to delegate linear-algebra primitives to Math.NET.
    /// </summary>
    internal static class MathNetBridge
    {
        /// <summary>
        /// Convert a 2-D <see cref="NdArray{T}"/> of <see cref="double"/> to a Math.NET dense matrix.
        /// Materializes a contiguous copy when the array is a non-contiguous view (e.g. a transpose).
        /// </summary>
        /// <param name="a">A 2-D ndarray.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 2-D.</exception>
        internal static Matrix<double> ToMathNetMatrix(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (a.Ndim != 2)
            {
                throw new ValueError($"expected 2-D array, got {a.Ndim}-D array");
            }

            int rows = a._shape[0];
            int cols = a._shape[1];

            // Math.NET DenseMatrix uses column-major storage. We have row-major data, so we
            // build via the indexed factory which lets us read elements through the
            // NdArray's stride layout (handling non-contiguous views like transposes).
            return DenseMatrix.OfIndexed(rows, cols, EnumerateMatrix(a));
        }

        private static System.Collections.Generic.IEnumerable<Tuple<int, int, double>> EnumerateMatrix(NdArray<double> a)
        {
            int rows = a._shape[0];
            int cols = a._shape[1];
            int rowStride = a._strides[0];
            int colStride = a._strides[1];
            int offset = a._offset;
            double[] data = a._data;

            for (int i = 0; i < rows; i++)
            {
                int rowBase = offset + i * rowStride;
                for (int j = 0; j < cols; j++)
                {
                    yield return Tuple.Create(i, j, data[rowBase + j * colStride]);
                }
            }
        }

        /// <summary>
        /// Convert a 1-D <see cref="NdArray{T}"/> of <see cref="double"/> to a Math.NET dense vector.
        /// Materializes a contiguous copy when the array is a non-contiguous view.
        /// </summary>
        /// <param name="a">A 1-D ndarray.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 1-D.</exception>
        internal static Vector<double> ToMathNetVector(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (a.Ndim != 1)
            {
                throw new ValueError($"expected 1-D array, got {a.Ndim}-D array");
            }

            int n = a._shape[0];
            var flat = new double[n];
            a.CopyToFlat(flat);
            return DenseVector.OfArray(flat);
        }

        /// <summary>
        /// Convert a Math.NET dense matrix to a 2-D row-major <see cref="NdArray{T}"/> of <see cref="double"/>.
        /// </summary>
        /// <param name="m">Source matrix.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="m"/> is null.</exception>
        internal static NdArray<double> FromMathNetMatrix(Matrix<double> m)
        {
            if (m == null)
            {
                throw new ArgumentNullException(nameof(m));
            }

            int rows = m.RowCount;
            int cols = m.ColumnCount;
            var data = new double[rows * cols];
            for (int i = 0; i < rows; i++)
            {
                int rowBase = i * cols;
                for (int j = 0; j < cols; j++)
                {
                    data[rowBase + j] = m[i, j];
                }
            }

            return new NdArray<double>(data, new[] { rows, cols });
        }

        /// <summary>
        /// Convert a Math.NET dense vector to a 1-D <see cref="NdArray{T}"/> of <see cref="double"/>.
        /// </summary>
        /// <param name="v">Source vector.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="v"/> is null.</exception>
        internal static NdArray<double> FromMathNetVector(Vector<double> v)
        {
            if (v == null)
            {
                throw new ArgumentNullException(nameof(v));
            }

            int n = v.Count;
            var data = new double[n];
            for (int i = 0; i < n; i++)
            {
                data[i] = v[i];
            }

            return new NdArray<double>(data, new[] { n });
        }
    }
}
