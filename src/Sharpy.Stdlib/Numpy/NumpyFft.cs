using System;
using MathNet.Numerics.IntegralTransforms;

// Note: we intentionally do NOT add `using System.Numerics;` here. The Sharpy namespace
// defines its own `Complex` type (Partial.Complex/Complex.cs) that shadows the BCL one
// at unqualified call sites. NdArray<T>'s `T : struct, IEquatable<T>` constraint is
// satisfied by System.Numerics.Complex but NOT by Sharpy.Complex, so we alias the BCL
// type explicitly and route the public API through it.
using BclComplex = System.Numerics.Complex;

namespace Sharpy
{
    /// <summary>
    /// NumPy-equivalent <c>numpy.fft</c> submodule. Provides 1-D discrete Fourier
    /// transforms backed by Math.NET's <see cref="Fourier"/> implementation.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="FourierOptions.AsymmetricScaling"/> (asymmetric scaling: forward divides by 1,
    /// inverse divides by n), which matches NumPy's <c>numpy.fft.fft</c>/<c>ifft</c> convention.
    /// </remarks>
    [SharpyModule("numpy.fft")]
    public static class NumpyFft
    {
        /// <summary>
        /// Compute the 1-D discrete Fourier transform of a real-valued ndarray.
        /// </summary>
        /// <param name="a">Input 1-D ndarray of real values.</param>
        /// <returns>A 1-D ndarray of complex values with the same length as the input.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 1-dimensional.</exception>
        public static NdArray<BclComplex> Fft(NdArray<double> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (a.Ndim != 1)
            {
                throw new ValueError($"fft input must be 1-dimensional, got ndim={a.Ndim}");
            }

            int n = a.Size;
            var buffer = new BclComplex[n];
            // Copy via stride-aware indexing so views work correctly.
            int offset = a._offset;
            int stride = n == 0 ? 0 : a._strides[0];
            for (int i = 0; i < n; i++)
            {
                buffer[i] = new BclComplex(a._data[offset + i * stride], 0.0);
            }

            Fourier.Forward(buffer, FourierOptions.AsymmetricScaling);
            return new NdArray<BclComplex>(buffer, new[] { n });
        }

        /// <summary>
        /// Compute the 1-D discrete Fourier transform of a complex-valued ndarray.
        /// </summary>
        public static NdArray<BclComplex> Fft(NdArray<BclComplex> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (a.Ndim != 1)
            {
                throw new ValueError($"fft input must be 1-dimensional, got ndim={a.Ndim}");
            }

            int n = a.Size;
            var buffer = new BclComplex[n];
            int offset = a._offset;
            int stride = n == 0 ? 0 : a._strides[0];
            for (int i = 0; i < n; i++)
            {
                buffer[i] = a._data[offset + i * stride];
            }

            Fourier.Forward(buffer, FourierOptions.AsymmetricScaling);
            return new NdArray<BclComplex>(buffer, new[] { n });
        }

        /// <summary>
        /// Compute the 1-D inverse discrete Fourier transform of a complex-valued ndarray.
        /// </summary>
        /// <param name="a">Input 1-D ndarray of complex values.</param>
        /// <returns>A 1-D ndarray of complex values with the same length as the input.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="a"/> is null.</exception>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 1-dimensional.</exception>
        public static NdArray<BclComplex> Ifft(NdArray<BclComplex> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (a.Ndim != 1)
            {
                throw new ValueError($"ifft input must be 1-dimensional, got ndim={a.Ndim}");
            }

            int n = a.Size;
            var buffer = new BclComplex[n];
            int offset = a._offset;
            int stride = n == 0 ? 0 : a._strides[0];
            for (int i = 0; i < n; i++)
            {
                buffer[i] = a._data[offset + i * stride];
            }

            Fourier.Inverse(buffer, FourierOptions.AsymmetricScaling);
            return new NdArray<BclComplex>(buffer, new[] { n });
        }

        /// <summary>
        /// Return the discrete Fourier transform sample frequencies for a transform of length <paramref name="n"/>.
        /// </summary>
        /// <param name="n">Window length. Must be non-negative.</param>
        /// <param name="d">Sample spacing (inverse of the sampling rate). Default 1.0.</param>
        /// <returns>
        /// A 1-D ndarray of length <paramref name="n"/>. Frequency bins are arranged in
        /// NumPy order: <c>[0, 1, ..., n/2-1, -n/2, ..., -1] / (d*n)</c> for even n,
        /// or <c>[0, 1, ..., (n-1)/2, -(n-1)/2, ..., -1] / (d*n)</c> for odd n.
        /// </returns>
        /// <exception cref="ValueError">Thrown when <paramref name="n"/> is negative.</exception>
        public static NdArray<double> Fftfreq(int n, double d = 1.0)
        {
            if (n < 0)
            {
                throw new ValueError($"n must be non-negative, got {n}");
            }

            var data = new double[n];
            if (n == 0)
            {
                return new NdArray<double>(data, new[] { 0 });
            }

            double scale = 1.0 / (d * n);
            int half = (n - 1) / 2 + 1;
            for (int i = 0; i < half; i++)
            {
                data[i] = i * scale;
            }

            for (int i = half; i < n; i++)
            {
                data[i] = (i - n) * scale;
            }

            return new NdArray<double>(data, new[] { n });
        }
    }
}
