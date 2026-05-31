using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Provides NumPy-equivalent array manipulation functions.
    /// </summary>
    public static partial class Numpy
    {
        /// <summary>
        /// Join a sequence of arrays along an existing <paramref name="axis"/>.
        /// All input arrays must have the same shape except along <paramref name="axis"/>.
        /// </summary>
        /// <param name="arrays">Arrays to concatenate. Must not be empty.</param>
        /// <param name="axis">Axis along which to concatenate. Default 0.</param>
        /// <returns>A new C-contiguous array.</returns>
        public static NdArray<double> Concatenate(NdArray<double>[] arrays, int axis = 0)
        {
            if (arrays == null)
            {
                throw new ArgumentNullException(nameof(arrays));
            }

            if (arrays.Length == 0)
            {
                throw new ArgumentException("need at least one array to concatenate", nameof(arrays));
            }

            int ndim = arrays[0].Ndim;
            int normalizedAxis = NormalizeAxisForRank(axis, ndim);

            // Validate every input has the same rank, and the same shape on every non-axis dim.
            int axisTotal = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                var arr = arrays[i];
                if (arr == null)
                {
                    throw new ArgumentNullException($"{nameof(arrays)}[{i}]");
                }

                if (arr.Ndim != ndim)
                {
                    throw new ArgumentException(
                        $"all input arrays must have the same number of dimensions, but array {i} has {arr.Ndim} != {ndim}",
                        nameof(arrays));
                }

                var shape = arr.Shape;
                for (int d = 0; d < ndim; d++)
                {
                    if (d == normalizedAxis)
                    {
                        continue;
                    }

                    if (shape[d] != arrays[0].Shape[d])
                    {
                        throw new ArgumentException(
                            $"all the input array dimensions except for the concatenation axis must match exactly, but along dimension {d} array {i} has size {shape[d]} != {arrays[0].Shape[d]}",
                            nameof(arrays));
                    }
                }

                axisTotal += shape[normalizedAxis];
            }

            int[] outShape = arrays[0].Shape;
            outShape[normalizedAxis] = axisTotal;
            int outSize = 1;
            for (int i = 0; i < outShape.Length; i++)
            {
                outSize = checked(outSize * outShape[i]);
            }

            var outData = new double[outSize];

            // Copy each input along the concatenation axis into the destination buffer.
            int writeOffsetAlongAxis = 0;
            foreach (var arr in arrays)
            {
                CopyArrayIntoDest(arr, outData, outShape, normalizedAxis, writeOffsetAlongAxis);
                writeOffsetAlongAxis += arr.Shape[normalizedAxis];
            }

            return new NdArray<double>(outData, outShape);
        }

        /// <summary>
        /// Join a sequence of arrays along a new axis. All inputs must have the same shape.
        /// The output has rank <c>ndim + 1</c>.
        /// </summary>
        /// <param name="arrays">Arrays to stack.</param>
        /// <param name="axis">Index of the new axis in the output. Default 0.</param>
        public static NdArray<double> Stack(NdArray<double>[] arrays, int axis = 0)
        {
            if (arrays == null)
            {
                throw new ArgumentNullException(nameof(arrays));
            }

            if (arrays.Length == 0)
            {
                throw new ArgumentException("need at least one array to stack", nameof(arrays));
            }

            int innerNdim = arrays[0].Ndim;
            int outRank = innerNdim + 1;
            int normalizedAxis = NormalizeAxisForRank(axis, outRank);

            // Every input must share the same shape.
            for (int i = 1; i < arrays.Length; i++)
            {
                if (arrays[i].Ndim != innerNdim)
                {
                    throw new ArgumentException(
                        $"all input arrays must have the same shape — array {i} has rank {arrays[i].Ndim}, expected {innerNdim}",
                        nameof(arrays));
                }

                for (int d = 0; d < innerNdim; d++)
                {
                    if (arrays[i].Shape[d] != arrays[0].Shape[d])
                    {
                        throw new ArgumentException(
                            $"all input arrays must have the same shape — array {i} has size {arrays[i].Shape[d]} at dim {d}, expected {arrays[0].Shape[d]}",
                            nameof(arrays));
                    }
                }
            }

            // Insert size = arrays.Length at `normalizedAxis` in each input's shape.
            int[] expandedShape = new int[outRank];
            int srcShapeIdx = 0;
            for (int d = 0; d < outRank; d++)
            {
                if (d == normalizedAxis)
                {
                    expandedShape[d] = 1;
                }
                else
                {
                    expandedShape[d] = arrays[0].Shape[srcShapeIdx++];
                }
            }

            // Reshape each input to the expanded shape, then concatenate along the new axis.
            var expanded = new NdArray<double>[arrays.Length];
            for (int i = 0; i < arrays.Length; i++)
            {
                expanded[i] = arrays[i].Reshape(expandedShape);
            }

            return Concatenate(expanded, normalizedAxis);
        }

        /// <summary>
        /// Stack arrays horizontally — along the second axis for 2-D inputs, along axis 0 for 1-D.
        /// </summary>
        public static NdArray<double> Hstack(NdArray<double>[] arrays)
        {
            if (arrays == null)
            {
                throw new ArgumentNullException(nameof(arrays));
            }

            if (arrays.Length == 0)
            {
                throw new ArgumentException("need at least one array to stack", nameof(arrays));
            }

            // For 1-D inputs, hstack is concatenation along axis 0.
            // For higher-rank inputs, it concatenates along axis 1.
            return arrays[0].Ndim == 1 ? Concatenate(arrays, 0) : Concatenate(arrays, 1);
        }

        /// <summary>
        /// Stack arrays vertically — along the first axis. For 1-D inputs they are promoted
        /// to row vectors (shape <c>(1, n)</c>) before stacking.
        /// </summary>
        public static NdArray<double> Vstack(NdArray<double>[] arrays)
        {
            if (arrays == null)
            {
                throw new ArgumentNullException(nameof(arrays));
            }

            if (arrays.Length == 0)
            {
                throw new ArgumentException("need at least one array to stack", nameof(arrays));
            }

            // Promote 1-D inputs to (1, n) so the concatenation along axis 0 makes sense.
            var promoted = new NdArray<double>[arrays.Length];
            for (int i = 0; i < arrays.Length; i++)
            {
                if (arrays[i].Ndim == 1)
                {
                    promoted[i] = arrays[i].Reshape(1, arrays[i].Shape[0]);
                }
                else
                {
                    promoted[i] = arrays[i];
                }
            }

            return Concatenate(promoted, 0);
        }

        /// <summary>
        /// Split <paramref name="a"/> along <paramref name="axis"/> at the given index boundaries,
        /// returning a list of sub-arrays. Mirrors NumPy's <c>numpy.split</c>.
        /// </summary>
        /// <param name="a">Input array.</param>
        /// <param name="indices">Sorted strictly-increasing list of split points.</param>
        /// <param name="axis">Axis along which to split. Default 0.</param>
        public static NdArray<double>[] Split(NdArray<double> a, int[] indices, int axis = 0)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            int normalizedAxis = NormalizeAxisForRank(axis, a.Ndim);
            int axisLen = a.Shape[normalizedAxis];

            // Build slice boundaries: 0, indices..., axisLen.
            var boundaries = new int[indices.Length + 2];
            boundaries[0] = 0;
            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                if (idx < 0)
                {
                    idx = 0;
                }

                if (idx > axisLen)
                {
                    idx = axisLen;
                }

                boundaries[i + 1] = idx;
            }

            boundaries[boundaries.Length - 1] = axisLen;

            var result = new NdArray<double>[indices.Length + 1];
            var sliceSpecs = new SliceSpec[a.Ndim];
            for (int p = 0; p < indices.Length + 1; p++)
            {
                for (int d = 0; d < a.Ndim; d++)
                {
                    sliceSpecs[d] = d == normalizedAxis
                        ? SliceSpec.Range(boundaries[p], boundaries[p + 1])
                        : SliceSpec.All;
                }

                result[p] = a.Slice(sliceSpecs).Copy();
            }

            return result;
        }

        /// <summary>
        /// Return an array whose elements are taken from <paramref name="x"/> where
        /// <paramref name="condition"/> is true, and <paramref name="y"/> otherwise.
        /// All three inputs are broadcast to a common shape.
        /// </summary>
        public static NdArray<double> Where(NdArray<bool> condition, NdArray<double> x, NdArray<double> y)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            int[] shape = Broadcasting.BroadcastShapes(condition.Shape, x.Shape);
            shape = Broadcasting.BroadcastShapes(shape, y.Shape);

            int total = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                total = checked(total * shape[i]);
            }

            var data = new double[total];
            var itc = new BroadcastedIterator<bool>(condition, shape);
            var itx = new BroadcastedIterator<double>(x, shape);
            var ity = new BroadcastedIterator<double>(y, shape);

            for (int i = 0; i < total; i++)
            {
                data[i] = itc.Current ? itx.Current : ity.Current;
                itc.MoveNext();
                itx.MoveNext();
                ity.MoveNext();
            }

            return new NdArray<double>(data, shape);
        }

        /// <summary>
        /// Clamp every element of <paramref name="a"/> to the interval <c>[min, max]</c>.
        /// </summary>
        public static NdArray<double> Clip(NdArray<double> a, double min, double max)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (min > max)
            {
                throw new ArgumentException($"min ({min}) must not exceed max ({max})", nameof(min));
            }

            var data = new double[a.Size];
            var iter = new BroadcastedIterator<double>(a, a.Shape);
            for (int i = 0; i < a.Size; i++)
            {
                double v = iter.Current;
                if (v < min)
                {
                    v = min;
                }
                else if (v > max)
                {
                    v = max;
                }

                data[i] = v;
                iter.MoveNext();
            }

            return new NdArray<double>(data, a.Shape);
        }

        // -- Internal helpers -------------------------------------------------------

        private static int NormalizeAxisForRank(int axis, int rank)
        {
            int a = axis < 0 ? axis + rank : axis;
            if (a < 0 || a >= rank)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(axis),
                    $"axis {axis} is out of bounds for array of dimension {rank}");
            }

            return a;
        }

        // Copy elements from `src` into `dest` at position `writeOffsetAlongAxis` along `axis`,
        // where `dest` has shape `destShape` in row-major order.
        private static void CopyArrayIntoDest(
            NdArray<double> src,
            double[] dest,
            int[] destShape,
            int axis,
            int writeOffsetAlongAxis)
        {
            int ndim = destShape.Length;

            // Compute row-major strides for the destination.
            var destStrides = new int[ndim];
            int stride = 1;
            for (int i = ndim - 1; i >= 0; i--)
            {
                destStrides[i] = stride;
                stride = checked(stride * destShape[i]);
            }

            // Walk src in row-major order, mapping each element to its destination offset.
            var srcShape = src.Shape;
            var index = new int[ndim];
            int srcSize = src.Size;

            for (int flat = 0; flat < srcSize; flat++)
            {
                // Compute the source offset using src's actual strides (which may include views).
                int srcOffset = src._offset;
                for (int d = 0; d < ndim; d++)
                {
                    srcOffset += index[d] * src._strides[d];
                }

                // Compute the destination offset: same index but with `writeOffsetAlongAxis`
                // shifting the position along the concat axis.
                int destOffset = 0;
                for (int d = 0; d < ndim; d++)
                {
                    int destIdx = d == axis ? index[d] + writeOffsetAlongAxis : index[d];
                    destOffset += destIdx * destStrides[d];
                }

                dest[destOffset] = src._data[srcOffset];

                // Increment N-D index in row-major order based on src shape.
                for (int d = ndim - 1; d >= 0; d--)
                {
                    index[d]++;
                    if (index[d] < srcShape[d])
                    {
                        break;
                    }

                    index[d] = 0;
                }
            }
        }
    }
}
