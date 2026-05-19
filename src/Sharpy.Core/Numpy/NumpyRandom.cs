using System;
using MathNet.Numerics.Distributions;

namespace Sharpy
{
    /// <summary>
    /// NumPy-equivalent <c>numpy.random</c> submodule. Provides pseudo-random number
    /// generation backed by <see cref="System.Random"/> and Math.NET Numerics distributions.
    /// </summary>
    /// <remarks>
    /// The underlying random number generator is <see cref="ThreadStaticAttribute">thread-static</see>
    /// — each thread sees its own instance. This matches the safer .NET model while remaining
    /// compatible with Python's non-thread-safe global RNG behavior. Call <see cref="Seed(int)"/>
    /// on a thread to obtain a reproducible sequence on that thread.
    /// </remarks>
    [SharpyModule("numpy.random")]
    public static class NumpyRandom
    {
        [ThreadStatic]
        private static System.Random? _rng;

        private static System.Random Rng => _rng ??= new System.Random();

        /// <summary>
        /// Seed the thread-local random number generator with <paramref name="seed"/>.
        /// </summary>
        /// <param name="seed">Seed value for the underlying <see cref="System.Random"/>.</param>
        public static void Seed(int seed)
        {
            _rng = new System.Random(seed);
        }

        /// <summary>
        /// Random samples from a uniform distribution over <c>[0, 1)</c>.
        /// </summary>
        /// <param name="shape">Shape of the result. May be empty (returns a 0-D scalar array).</param>
        public static NdArray<double> Rand(params int[] shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            int size = ProductOfShape(shape);
            var data = new double[size];
            var rng = Rng;
            for (int i = 0; i < size; i++)
            {
                data[i] = rng.NextDouble();
            }

            return new NdArray<double>(data, shape);
        }

        /// <summary>
        /// Random samples from the standard normal distribution (mean 0, stddev 1).
        /// </summary>
        /// <param name="shape">Shape of the result.</param>
        public static NdArray<double> Randn(params int[] shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            int size = ProductOfShape(shape);
            var data = new double[size];
            var rng = Rng;
            for (int i = 0; i < size; i++)
            {
                data[i] = MathNet.Numerics.Distributions.Normal.Sample(rng, 0.0, 1.0);
            }

            return new NdArray<double>(data, shape);
        }

        /// <summary>
        /// Random integers from the half-open interval <c>[low, high)</c>.
        /// </summary>
        /// <param name="low">Inclusive lower bound.</param>
        /// <param name="high">Exclusive upper bound. Must be greater than <paramref name="low"/>.</param>
        /// <param name="shape">Shape of the result.</param>
        /// <exception cref="ValueError">Thrown when <paramref name="high"/> is not greater than <paramref name="low"/>.</exception>
        public static NdArray<int> Randint(int low, int high, int[] shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (high <= low)
            {
                throw new ValueError($"high ({high}) must be greater than low ({low})");
            }

            int size = ProductOfShape(shape);
            var data = new int[size];
            var rng = Rng;
            for (int i = 0; i < size; i++)
            {
                data[i] = rng.Next(low, high);
            }

            return new NdArray<int>(data, shape);
        }

        /// <summary>
        /// Random samples from a normal (Gaussian) distribution with the given mean and standard deviation.
        /// </summary>
        /// <param name="loc">Mean (<c>mu</c>) of the distribution.</param>
        /// <param name="scale">Standard deviation (<c>sigma</c>) of the distribution. Must be non-negative.</param>
        /// <param name="shape">Shape of the result.</param>
        /// <exception cref="ValueError">Thrown when <paramref name="scale"/> is negative.</exception>
        public static NdArray<double> Normal(double loc, double scale, int[] shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (scale < 0.0)
            {
                throw new ValueError($"scale must be non-negative, got {scale}");
            }

            int size = ProductOfShape(shape);
            var data = new double[size];
            var rng = Rng;
            for (int i = 0; i < size; i++)
            {
                data[i] = MathNet.Numerics.Distributions.Normal.Sample(rng, loc, scale);
            }

            return new NdArray<double>(data, shape);
        }

        /// <summary>
        /// Random samples from a continuous uniform distribution over <c>[low, high)</c>.
        /// </summary>
        /// <param name="low">Inclusive lower bound.</param>
        /// <param name="high">Exclusive upper bound. Must be greater than or equal to <paramref name="low"/>.</param>
        /// <param name="shape">Shape of the result.</param>
        /// <exception cref="ValueError">Thrown when <paramref name="high"/> is less than <paramref name="low"/>.</exception>
        public static NdArray<double> Uniform(double low, double high, int[] shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (high < low)
            {
                throw new ValueError($"high ({high}) must be greater than or equal to low ({low})");
            }

            int size = ProductOfShape(shape);
            var data = new double[size];
            var rng = Rng;
            for (int i = 0; i < size; i++)
            {
                data[i] = ContinuousUniform.Sample(rng, low, high);
            }

            return new NdArray<double>(data, shape);
        }

        /// <summary>
        /// Draw <paramref name="size"/> random samples from a 1-D ndarray <paramref name="a"/>.
        /// </summary>
        /// <typeparam name="T">Element type of the source array.</typeparam>
        /// <param name="a">Source 1-D ndarray to sample from.</param>
        /// <param name="size">Number of samples to draw. Must be non-negative.</param>
        /// <param name="replace">Whether sampling is with replacement. Default <c>true</c>.
        /// When <c>false</c>, <paramref name="size"/> must not exceed <c>a.Size</c>.</param>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is not 1-D, when <paramref name="size"/> is negative,
        /// when <paramref name="a"/> is empty and <paramref name="size"/> &gt; 0, or when sampling without replacement and
        /// <paramref name="size"/> exceeds the source length.</exception>
        public static NdArray<T> Choice<T>(NdArray<T> a, int size, bool replace = true)
            where T : struct, IEquatable<T>
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (a.Ndim != 1)
            {
                throw new ValueError($"a must be 1-dimensional, got ndim={a.Ndim}");
            }

            if (size < 0)
            {
                throw new ValueError($"size must be non-negative, got {size}");
            }

            int n = a.Size;
            if (size > 0 && n == 0)
            {
                throw new ValueError("a must be non-empty when size > 0");
            }

            if (!replace && size > n)
            {
                throw new ValueError(
                    $"cannot take a larger sample than population when replace=false (size={size}, n={n})");
            }

            var data = new T[size];
            var rng = Rng;

            // Materialize source values via flat stride-aware indexing so views work correctly.
            var source = new T[n];
            for (int i = 0; i < n; i++)
            {
                source[i] = a._data[a._offset + i * a._strides[0]];
            }

            if (replace)
            {
                for (int i = 0; i < size; i++)
                {
                    data[i] = source[rng.Next(n)];
                }
            }
            else
            {
                // Partial Fisher-Yates: draw without replacement by shuffling the first `size` slots.
                var indices = new int[n];
                for (int i = 0; i < n; i++)
                {
                    indices[i] = i;
                }

                for (int i = 0; i < size; i++)
                {
                    int j = rng.Next(i, n);
                    int tmp = indices[i];
                    indices[i] = indices[j];
                    indices[j] = tmp;
                    data[i] = source[indices[i]];
                }
            }

            return new NdArray<T>(data, new[] { size });
        }

        /// <summary>
        /// Shuffle the contents of <paramref name="a"/> in place along its first axis.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="a">Array to shuffle. For multi-dimensional arrays, contiguous row blocks
        /// of <c>a.Shape[1..]</c> are permuted as units (matches NumPy semantics).</param>
        /// <exception cref="ValueError">Thrown when <paramref name="a"/> is 0-dimensional.</exception>
        public static void Shuffle<T>(NdArray<T> a) where T : struct, IEquatable<T>
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (a.Ndim == 0)
            {
                throw new ValueError("cannot shuffle a 0-dimensional array");
            }

            int n = a.Shape[0];
            if (n <= 1)
            {
                return;
            }

            // Block size in elements along axis 0.
            int blockSize = 1;
            for (int d = 1; d < a.Ndim; d++)
            {
                blockSize *= a.Shape[d];
            }

            int stride0 = a._strides[0];
            int offset = a._offset;
            var rng = Rng;

            // Fisher-Yates swap of full blocks along axis 0.
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                if (i == j)
                {
                    continue;
                }

                int srcBase = offset + i * stride0;
                int dstBase = offset + j * stride0;

                if (a.Ndim == 1)
                {
                    T tmp = a._data[srcBase];
                    a._data[srcBase] = a._data[dstBase];
                    a._data[dstBase] = tmp;
                }
                else
                {
                    SwapBlock(a, srcBase, dstBase, blockSize);
                }
            }
        }

        private static void SwapBlock<T>(NdArray<T> a, int srcBase, int dstBase, int blockSize)
            where T : struct, IEquatable<T>
        {
            // Walk the trailing axes in row-major order. For contiguous trailing dims this
            // is simply a flat copy; we use stride-aware iteration to be safe with views.
            int rank = a.Ndim;
            var idx = new int[rank - 1];
            for (int k = 0; k < blockSize; k++)
            {
                int srcOffset = srcBase;
                int dstOffset = dstBase;
                for (int d = 0; d < rank - 1; d++)
                {
                    srcOffset += idx[d] * a._strides[d + 1];
                    dstOffset += idx[d] * a._strides[d + 1];
                }

                T tmp = a._data[srcOffset];
                a._data[srcOffset] = a._data[dstOffset];
                a._data[dstOffset] = tmp;

                // Increment row-major index.
                for (int d = rank - 2; d >= 0; d--)
                {
                    idx[d]++;
                    if (idx[d] < a.Shape[d + 1])
                    {
                        break;
                    }

                    idx[d] = 0;
                }
            }
        }

        private static int ProductOfShape(int[] shape)
        {
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
