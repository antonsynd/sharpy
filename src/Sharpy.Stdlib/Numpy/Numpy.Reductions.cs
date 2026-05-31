using System;

namespace Sharpy
{
    /// <summary>
    /// Provides NumPy-equivalent array reduction functions (sum, min, max, mean, variance, etc.).
    /// </summary>
    public static partial class Numpy
    {
        // -- Full reductions (collapse the entire array to a scalar) -----------------

        /// <summary>Sum of all elements.</summary>
        public static double Sum(NdArray<double> a) => ReduceAll(a, 0.0, (acc, x) => acc + x);

        /// <summary>Minimum element. Throws when <paramref name="a"/> is empty.</summary>
        public static double Min(NdArray<double> a)
        {
            RequireNonEmpty(a, nameof(Min));
            return ReduceAll(a, double.PositiveInfinity, System.Math.Min);
        }

        /// <summary>Maximum element. Throws when <paramref name="a"/> is empty.</summary>
        public static double Max(NdArray<double> a)
        {
            RequireNonEmpty(a, nameof(Max));
            return ReduceAll(a, double.NegativeInfinity, System.Math.Max);
        }

        /// <summary>Arithmetic mean. Throws when <paramref name="a"/> is empty.</summary>
        public static double Mean(NdArray<double> a)
        {
            RequireNonEmpty(a, nameof(Mean));
            return Sum(a) / a.Size;
        }

        /// <summary>Population variance (ddof = 0). Throws when <paramref name="a"/> is empty.</summary>
        public static double Var(NdArray<double> a)
        {
            RequireNonEmpty(a, nameof(Var));
            double m = Mean(a);
            double sumSq = 0.0;
            var iter = new BroadcastedIterator<double>(a, a.Shape);
            for (int i = 0; i < a.Size; i++)
            {
                double d = iter.Current - m;
                sumSq += d * d;
                iter.MoveNext();
            }

            return sumSq / a.Size;
        }

        /// <summary>Population standard deviation (ddof = 0). Throws when <paramref name="a"/> is empty.</summary>
        public static double Std(NdArray<double> a) => System.Math.Sqrt(Var(a));

        /// <summary>Median of all elements. Throws when <paramref name="a"/> is empty.</summary>
        public static double Median(NdArray<double> a)
        {
            RequireNonEmpty(a, nameof(Median));

            var copy = new double[a.Size];
            var iter = new BroadcastedIterator<double>(a, a.Shape);
            for (int i = 0; i < a.Size; i++)
            {
                copy[i] = iter.Current;
                iter.MoveNext();
            }

            System.Array.Sort(copy);
            int n = copy.Length;
            if ((n & 1) == 1)
            {
                return copy[n / 2];
            }

            return 0.5 * (copy[n / 2 - 1] + copy[n / 2]);
        }

        // -- Along-axis reductions (produce an array with one less dimension) -------

        /// <summary>Sum along <paramref name="axis"/>, removing that dimension.</summary>
        public static NdArray<double> Sum(NdArray<double> a, int axis) =>
            ReduceAlongAxis(a, axis, _ => 0.0, (acc, x, _) => acc + x, (acc, _) => acc);

        /// <summary>Minimum along <paramref name="axis"/>, removing that dimension.</summary>
        public static NdArray<double> Min(NdArray<double> a, int axis)
        {
            RequireNonEmptyAxis(a, axis, nameof(Min));
            return ReduceAlongAxis(
                a, axis,
                _ => double.PositiveInfinity,
                (acc, x, _) => System.Math.Min(acc, x),
                (acc, _) => acc);
        }

        /// <summary>Maximum along <paramref name="axis"/>, removing that dimension.</summary>
        public static NdArray<double> Max(NdArray<double> a, int axis)
        {
            RequireNonEmptyAxis(a, axis, nameof(Max));
            return ReduceAlongAxis(
                a, axis,
                _ => double.NegativeInfinity,
                (acc, x, _) => System.Math.Max(acc, x),
                (acc, _) => acc);
        }

        /// <summary>Mean along <paramref name="axis"/>, removing that dimension.</summary>
        public static NdArray<double> Mean(NdArray<double> a, int axis)
        {
            RequireNonEmptyAxis(a, axis, nameof(Mean));
            return ReduceAlongAxis(
                a, axis,
                _ => 0.0,
                (acc, x, _) => acc + x,
                (acc, n) => acc / n);
        }

        /// <summary>Population variance along <paramref name="axis"/>, removing that dimension.</summary>
        public static NdArray<double> Var(NdArray<double> a, int axis)
        {
            RequireNonEmptyAxis(a, axis, nameof(Var));

            // Two-pass: compute mean per slice, then sum of squared deviations.
            var means = Mean(a, axis);
            return ReduceAlongAxisVariance(a, axis, means);
        }

        /// <summary>Population standard deviation along <paramref name="axis"/>.</summary>
        public static NdArray<double> Std(NdArray<double> a, int axis)
        {
            var v = Var(a, axis);
            return Sqrt(v);
        }

        /// <summary>Median along <paramref name="axis"/>, removing that dimension.</summary>
        public static NdArray<double> Median(NdArray<double> a, int axis)
        {
            RequireNonEmptyAxis(a, axis, nameof(Median));

            int normalizedAxis = NormalizeAxis(axis, a.Shape.Length);
            int axisLen = a.Shape[normalizedAxis];
            int[] outShape = ShapeWithoutAxis(a.Shape, normalizedAxis);

            int outSize = ProductOfShapeChecked(outShape);
            var outData = new double[outSize];
            var buffer = new double[axisLen];

            ForEachOuterIndex(a, normalizedAxis, (outerIdx, outerFlat) =>
            {
                // Gather slice along the axis.
                for (int k = 0; k < axisLen; k++)
                {
                    buffer[k] = ElementAtAxis(a, normalizedAxis, outerIdx, k);
                }

                System.Array.Sort(buffer);
                double m;
                if ((axisLen & 1) == 1)
                {
                    m = buffer[axisLen / 2];
                }
                else
                {
                    m = 0.5 * (buffer[axisLen / 2 - 1] + buffer[axisLen / 2]);
                }

                outData[outerFlat] = m;
            });

            return new NdArray<double>(outData, outShape);
        }

        // -- Internal helpers -------------------------------------------------------

        private static void RequireNonEmpty(NdArray<double> a, string opName)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (a.Size == 0)
            {
                throw new InvalidOperationException(
                    $"zero-size array to reduction operation {opName} which has no identity");
            }
        }

        private static void RequireNonEmptyAxis(NdArray<double> a, int axis, string opName)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            int normalized = NormalizeAxis(axis, a.Shape.Length);
            if (a.Shape[normalized] == 0)
            {
                throw new InvalidOperationException(
                    $"zero-size axis {axis} for reduction operation {opName} which has no identity");
            }
        }

        private static double ReduceAll(NdArray<double> a, double seed, Func<double, double, double> step)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            double acc = seed;
            var iter = new BroadcastedIterator<double>(a, a.Shape);
            for (int i = 0; i < a.Size; i++)
            {
                acc = step(acc, iter.Current);
                iter.MoveNext();
            }

            return acc;
        }

        private static int NormalizeAxis(int axis, int ndim)
        {
            int a = axis < 0 ? axis + ndim : axis;
            if (a < 0 || a >= ndim)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(axis),
                    $"axis {axis} is out of bounds for array of dimension {ndim}");
            }

            return a;
        }

        private static int[] ShapeWithoutAxis(int[] shape, int axis)
        {
            var result = new int[shape.Length - 1];
            for (int i = 0, j = 0; i < shape.Length; i++)
            {
                if (i == axis)
                {
                    continue;
                }

                result[j++] = shape[i];
            }

            return result;
        }

        // Walk every "outer" index — every combination of the non-reduced axes — and provide
        // both the multi-index and the flat offset into the row-major output buffer.
        private static void ForEachOuterIndex(NdArray<double> a, int axis, Action<int[], int> callback)
        {
            int ndim = a.Shape.Length;
            int[] outShape = ShapeWithoutAxis(a.Shape, axis);
            int outerSize = ProductOfShapeChecked(outShape);
            int outerNdim = outShape.Length;

            var multi = new int[ndim];
            var outerIdx = new int[outerNdim];

            for (int flat = 0; flat < outerSize; flat++)
            {
                int rem = flat;
                for (int k = outerNdim - 1; k >= 0; k--)
                {
                    int dim = outShape[k];
                    outerIdx[k] = dim == 0 ? 0 : rem % dim;
                    rem = dim == 0 ? 0 : rem / dim;
                }

                // Project outerIdx onto a full multi-index (with 0 along the reduced axis).
                for (int i = 0, j = 0; i < ndim; i++)
                {
                    multi[i] = i == axis ? 0 : outerIdx[j++];
                }

                callback(multi, flat);
            }
        }

        private static double ElementAtAxis(NdArray<double> a, int axis, int[] outerMulti, int k)
        {
            // outerMulti is sized to ndim, with 0 in the reduced axis slot; substitute k.
            int offset = a._offset;
            for (int i = 0; i < a._shape.Length; i++)
            {
                int idx = i == axis ? k : outerMulti[i];
                offset += idx * a._strides[i];
            }

            return a._data[offset];
        }

        private static NdArray<double> ReduceAlongAxis(
            NdArray<double> a,
            int axis,
            Func<int, double> seedFn,
            Func<double, double, int, double> step,
            Func<double, int, double> finalize)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            int normalizedAxis = NormalizeAxis(axis, a.Shape.Length);
            int axisLen = a.Shape[normalizedAxis];
            int[] outShape = ShapeWithoutAxis(a.Shape, normalizedAxis);
            int outSize = ProductOfShapeChecked(outShape);
            var outData = new double[outSize];

            ForEachOuterIndex(a, normalizedAxis, (outerIdx, outerFlat) =>
            {
                double acc = seedFn(axisLen);
                for (int k = 0; k < axisLen; k++)
                {
                    double v = ElementAtAxis(a, normalizedAxis, outerIdx, k);
                    acc = step(acc, v, k);
                }

                outData[outerFlat] = finalize(acc, axisLen);
            });

            return new NdArray<double>(outData, outShape);
        }

        private static NdArray<double> ReduceAlongAxisVariance(NdArray<double> a, int axis, NdArray<double> means)
        {
            int normalizedAxis = NormalizeAxis(axis, a.Shape.Length);
            int axisLen = a.Shape[normalizedAxis];
            int[] outShape = ShapeWithoutAxis(a.Shape, normalizedAxis);
            int outSize = ProductOfShapeChecked(outShape);
            var outData = new double[outSize];

            ForEachOuterIndex(a, normalizedAxis, (outerIdx, outerFlat) =>
            {
                double m = means._data[means._offset + GetOuterFlatOffset(means, outerIdx, normalizedAxis)];
                double sumSq = 0.0;
                for (int k = 0; k < axisLen; k++)
                {
                    double d = ElementAtAxis(a, normalizedAxis, outerIdx, k) - m;
                    sumSq += d * d;
                }

                outData[outerFlat] = sumSq / axisLen;
            });

            return new NdArray<double>(outData, outShape);
        }

        // Given an outer multi-index sized to a.Ndim (with placeholder 0 in the reduced axis),
        // compute the flat offset into the reduced-shape result `means`.
        private static int GetOuterFlatOffset(NdArray<double> reduced, int[] outerMulti, int reducedAxis)
        {
            int offset = 0;
            for (int i = 0, j = 0; i < outerMulti.Length; i++)
            {
                if (i == reducedAxis)
                {
                    continue;
                }

                offset += outerMulti[i] * reduced._strides[j];
                j++;
            }

            return offset;
        }
    }
}
