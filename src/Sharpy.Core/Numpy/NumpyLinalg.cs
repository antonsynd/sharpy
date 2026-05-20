using System;
using MathNet.Numerics.LinearAlgebra;

namespace Sharpy
{
    /// <summary>
    /// Linear-algebra submodule mirroring <c>numpy.linalg</c>. Delegates to Math.NET Numerics
    /// for matrix multiplication, decomposition, and inversion primitives.
    /// </summary>
    [SharpyModule("numpy.linalg")]
    public static class NumpyLinalg
    {
        /// <summary>
        /// Dot product of two arrays.
        ///   * 1-D × 1-D — inner product (scalar) returned as a 0-D ndarray.
        ///   * 2-D × 2-D — standard matrix multiplication.
        ///   * 2-D × 1-D — matrix-vector product.
        ///   * 1-D × 2-D — vector-matrix product (treats vector as a row).
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> or <paramref name="b"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when shapes are incompatible or rank is unsupported.</exception>
        public static NdArray<double> Dot(NdArray<double> a, NdArray<double> b)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            try
            {
                if (a.Ndim == 1 && b.Ndim == 1)
                {
                    var va = MathNetBridge.ToMathNetVector(a);
                    var vb = MathNetBridge.ToMathNetVector(b);
                    if (va.Count != vb.Count)
                    {
                        throw new ValueError($"shapes ({va.Count},) and ({vb.Count},) not aligned");
                    }

                    double scalar = va.DotProduct(vb);
                    return new NdArray<double>(new[] { scalar }, System.Array.Empty<int>());
                }

                if (a.Ndim == 2 && b.Ndim == 2)
                {
                    var ma = MathNetBridge.ToMathNetMatrix(a);
                    var mb = MathNetBridge.ToMathNetMatrix(b);
                    if (ma.ColumnCount != mb.RowCount)
                    {
                        throw new ValueError(
                            $"shapes ({ma.RowCount},{ma.ColumnCount}) and ({mb.RowCount},{mb.ColumnCount}) not aligned");
                    }

                    return MathNetBridge.FromMathNetMatrix(ma * mb);
                }

                if (a.Ndim == 2 && b.Ndim == 1)
                {
                    var ma = MathNetBridge.ToMathNetMatrix(a);
                    var vb = MathNetBridge.ToMathNetVector(b);
                    if (ma.ColumnCount != vb.Count)
                    {
                        throw new ValueError(
                            $"shapes ({ma.RowCount},{ma.ColumnCount}) and ({vb.Count},) not aligned");
                    }

                    return MathNetBridge.FromMathNetVector(ma * vb);
                }

                if (a.Ndim == 1 && b.Ndim == 2)
                {
                    var va = MathNetBridge.ToMathNetVector(a);
                    var mb = MathNetBridge.ToMathNetMatrix(b);
                    if (va.Count != mb.RowCount)
                    {
                        throw new ValueError(
                            $"shapes ({va.Count},) and ({mb.RowCount},{mb.ColumnCount}) not aligned");
                    }

                    return MathNetBridge.FromMathNetVector(va * mb);
                }

                throw new ValueError($"dot is only implemented for arrays of rank <= 2 (got {a.Ndim} and {b.Ndim})");
            }
            catch (ArgumentException ex)
            {
                throw new ValueError(ex.Message, ex);
            }
        }

        /// <summary>
        /// Matrix product. For 1-D and 2-D inputs this is equivalent to <see cref="Dot"/>.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static NdArray<double> Matmul(NdArray<double> a, NdArray<double> b) => Dot(a, b);

        /// <summary>
        /// Compute the (multiplicative) inverse of a square matrix.
        /// </summary>
        /// <param name="a">A square 2-D array.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 2-D, not square, or is singular.</exception>
        public static NdArray<double> Inv(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var m = MathNetBridge.ToMathNetMatrix(a);
            if (m.RowCount != m.ColumnCount)
            {
                throw new ValueError($"expected square matrix, got ({m.RowCount},{m.ColumnCount})");
            }

            double det = m.Determinant();
            if (double.IsNaN(det) || double.IsInfinity(det) || System.Math.Abs(det) < 1e-15)
            {
                throw new ValueError("singular matrix");
            }

            try
            {
                var inv = m.Inverse();
                return MathNetBridge.FromMathNetMatrix(inv);
            }
            catch (InvalidOperationException ex)
            {
                throw new ValueError("singular matrix", ex);
            }
            catch (ArgumentException ex)
            {
                throw new ValueError(ex.Message, ex);
            }
        }

        /// <summary>
        /// Compute the determinant of a square 2-D array.
        /// </summary>
        /// <param name="a">A square 2-D array.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 2-D or not square.</exception>
        public static double Det(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var m = MathNetBridge.ToMathNetMatrix(a);
            if (m.RowCount != m.ColumnCount)
            {
                throw new ValueError($"expected square matrix, got ({m.RowCount},{m.ColumnCount})");
            }

            return m.Determinant();
        }

        /// <summary>
        /// Compute the eigenvalues and (right) eigenvectors of a square 2-D array.
        /// </summary>
        /// <param name="a">A square 2-D array.</param>
        /// <returns>
        /// A tuple <c>(eigenvalues, eigenvectors)</c> where <c>eigenvalues</c> is a 1-D ndarray and
        /// <c>eigenvectors</c> is a 2-D ndarray whose columns are the eigenvectors. Imaginary parts
        /// of complex eigenvalues are dropped — only the real component is returned.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 2-D or not square.</exception>
        public static (NdArray<double> eigenvalues, NdArray<double> eigenvectors) Eig(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var m = MathNetBridge.ToMathNetMatrix(a);
            if (m.RowCount != m.ColumnCount)
            {
                throw new ValueError($"expected square matrix, got ({m.RowCount},{m.ColumnCount})");
            }

            try
            {
                var evd = m.Evd();

                int n = m.RowCount;
                var values = new double[n];
                var complexValues = evd.EigenValues;
                for (int i = 0; i < n; i++)
                {
                    values[i] = complexValues[i].Real;
                }

                var vectors = MathNetBridge.FromMathNetMatrix(evd.EigenVectors);
                return (new NdArray<double>(values, new[] { n }), vectors);
            }
            catch (ArgumentException ex)
            {
                throw new ValueError(ex.Message, ex);
            }
        }

        /// <summary>
        /// Singular value decomposition. Returns <c>(U, S, Vh)</c> such that <c>A = U · diag(S) · Vh</c>.
        /// </summary>
        /// <param name="a">A 2-D array.</param>
        /// <returns>
        /// A tuple <c>(U, S, Vh)</c> where <c>U</c> and <c>Vh</c> are 2-D ndarrays and <c>S</c>
        /// is a 1-D ndarray of singular values in descending order.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 2-D.</exception>
        public static (NdArray<double> U, NdArray<double> S, NdArray<double> Vh) Svd(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var m = MathNetBridge.ToMathNetMatrix(a);

            try
            {
                var svd = m.Svd(computeVectors: true);
                var u = MathNetBridge.FromMathNetMatrix(svd.U);
                var s = MathNetBridge.FromMathNetVector(svd.S);
                var vh = MathNetBridge.FromMathNetMatrix(svd.VT);
                return (u, s, vh);
            }
            catch (ArgumentException ex)
            {
                throw new ValueError(ex.Message, ex);
            }
        }

        /// <summary>
        /// Solve the linear system <c>A x = b</c> for <c>x</c>.
        /// </summary>
        /// <param name="a">Coefficient matrix (square 2-D array).</param>
        /// <param name="b">Right-hand side. Either a 1-D vector or a 2-D matrix.</param>
        /// <returns>
        /// Solution with the same rank as <paramref name="b"/> (1-D ndarray when <paramref name="b"/>
        /// is 1-D, 2-D ndarray otherwise).
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> or <paramref name="b"/> is null.</exception>
        /// <exception cref="ValueError">
        /// Thrown when shapes are incompatible, the matrix is singular, or the rank is unsupported.
        /// </exception>
        public static NdArray<double> Solve(NdArray<double> a, NdArray<double> b)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            var m = MathNetBridge.ToMathNetMatrix(a);
            if (m.RowCount != m.ColumnCount)
            {
                throw new ValueError($"expected square coefficient matrix, got ({m.RowCount},{m.ColumnCount})");
            }

            double det = m.Determinant();
            if (double.IsNaN(det) || double.IsInfinity(det) || System.Math.Abs(det) < 1e-15)
            {
                throw new ValueError("singular matrix");
            }

            try
            {
                if (b.Ndim == 1)
                {
                    var rhs = MathNetBridge.ToMathNetVector(b);
                    if (rhs.Count != m.RowCount)
                    {
                        throw new ValueError(
                            $"shapes ({m.RowCount},{m.ColumnCount}) and ({rhs.Count},) not aligned");
                    }

                    return MathNetBridge.FromMathNetVector(m.Solve(rhs));
                }

                if (b.Ndim == 2)
                {
                    var rhs = MathNetBridge.ToMathNetMatrix(b);
                    if (rhs.RowCount != m.RowCount)
                    {
                        throw new ValueError(
                            $"shapes ({m.RowCount},{m.ColumnCount}) and ({rhs.RowCount},{rhs.ColumnCount}) not aligned");
                    }

                    return MathNetBridge.FromMathNetMatrix(m.Solve(rhs));
                }

                throw new ValueError($"right-hand side must be 1-D or 2-D, got {b.Ndim}-D");
            }
            catch (InvalidOperationException ex)
            {
                throw new ValueError("singular matrix", ex);
            }
            catch (ArgumentException ex)
            {
                throw new ValueError(ex.Message, ex);
            }
        }

        /// <summary>
        /// Compute the L2 (Frobenius) norm of an array.
        ///   * 1-D — Euclidean (L2) norm.
        ///   * 2-D — Frobenius norm.
        /// </summary>
        /// <param name="a">Input array.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 1-D or 2-D.</exception>
        public static double Norm(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (a.Ndim == 1)
            {
                return MathNetBridge.ToMathNetVector(a).L2Norm();
            }

            if (a.Ndim == 2)
            {
                return MathNetBridge.ToMathNetMatrix(a).FrobeniusNorm();
            }

            throw new ValueError($"norm is only implemented for arrays of rank 1 or 2 (got {a.Ndim})");
        }
    }
}
