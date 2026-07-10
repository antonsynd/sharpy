using System;

namespace Sharpy
{
    public partial class NdArray<T>
    {
        /// <summary>
        /// Matrix multiplication (<c>@</c>, PEP 465). Delegates to
        /// <see cref="NumpyLinalg.Matmul(NdArray{double}, NdArray{double})"/>, which follows
        /// NumPy's dot semantics (inner product for 1-D operands, matrix product for 2-D).
        /// </summary>
        /// <param name="other">The right-hand operand.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        /// <exception cref="TypeError">
        /// Thrown when the arrays are not <c>float64</c> (<c>double</c>). NumPy's linear-algebra
        /// surface here is defined only for floating-point arrays.
        /// </exception>
        public NdArray<T> MatMul(NdArray<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (this is NdArray<double> a && other is NdArray<double> b)
            {
                return (NdArray<T>)(object)NumpyLinalg.Matmul(a, b);
            }

            throw new TypeError(
                "matrix multiplication (@) is only supported for float64 arrays");
        }
    }
}
