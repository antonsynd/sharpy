using System;

namespace Sharpy
{
    public partial class NdArray<T>
    {
        /// <summary>
        /// Take elements from this array at the positions given by <paramref name="indices"/>.
        /// For a 1-D source this returns a 1-D array of the selected values; for higher-rank
        /// sources this selects entire (N-1)-D slices along <paramref name="axis"/>.
        /// </summary>
        /// <param name="indices">Integer indices into <paramref name="axis"/>. Negative values follow Python semantics.</param>
        /// <param name="axis">Axis along which to select. Default 0.</param>
        /// <returns>A new C-contiguous array of the selected elements.</returns>
        public NdArray<T> Take(int[] indices, int axis = 0)
        {
            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            int normalizedAxis = NormalizeAxis(axis, _shape.Length);
            int axisLen = _shape[normalizedAxis];

            // Build the output shape: same as input, with axis dim replaced by indices.Length.
            var outShape = new int[_shape.Length];
            System.Array.Copy(_shape, outShape, _shape.Length);
            outShape[normalizedAxis] = indices.Length;

            int outSize = 1;
            for (int i = 0; i < outShape.Length; i++)
            {
                outSize = checked(outSize * outShape[i]);
            }

            // Pre-resolve negative indices and validate.
            var resolved = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                if (idx < 0)
                {
                    idx += axisLen;
                }

                if (idx < 0 || idx >= axisLen)
                {
                    throw new IndexError(
                        $"index {indices[i]} is out of bounds for axis {axis} with size {axisLen}");
                }

                resolved[i] = idx;
            }

            var outData = new T[outSize];

            // Walk the output in row-major order, mapping each position back to the source.
            int ndim = _shape.Length;
            var outIndex = new int[ndim];
            for (int flat = 0; flat < outSize; flat++)
            {
                int srcOffset = _offset;
                for (int d = 0; d < ndim; d++)
                {
                    int idxIntoSrc = d == normalizedAxis ? resolved[outIndex[d]] : outIndex[d];
                    srcOffset += idxIntoSrc * _strides[d];
                }

                outData[flat] = _data[srcOffset];

                // Increment row-major index against outShape.
                for (int d = ndim - 1; d >= 0; d--)
                {
                    outIndex[d]++;
                    if (outIndex[d] < outShape[d])
                    {
                        break;
                    }

                    outIndex[d] = 0;
                }
            }

            return new NdArray<T>(outData, outShape);
        }

        /// <summary>
        /// Write <paramref name="values"/> into this array at the positions given by
        /// <paramref name="indices"/> along <paramref name="axis"/>. The shape of
        /// <paramref name="values"/> must match the shape of <see cref="Take"/>'s result
        /// for the same indices/axis.
        /// </summary>
        public void Put(int[] indices, NdArray<T> values, int axis = 0)
        {
            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            int normalizedAxis = NormalizeAxis(axis, _shape.Length);
            int axisLen = _shape[normalizedAxis];

            // Resolve and validate indices.
            var resolved = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                if (idx < 0)
                {
                    idx += axisLen;
                }

                if (idx < 0 || idx >= axisLen)
                {
                    throw new IndexError(
                        $"index {indices[i]} is out of bounds for axis {axis} with size {axisLen}");
                }

                resolved[i] = idx;
            }

            // Build the implicit "target" shape — same as ours with axis dim replaced by len.
            var targetShape = new int[_shape.Length];
            System.Array.Copy(_shape, targetShape, _shape.Length);
            targetShape[normalizedAxis] = indices.Length;

            // Use a broadcasted iterator over `values` so the caller can pass a scalar-like 1-D
            // values array and have it spread across higher-rank slots.
            int ndim = _shape.Length;
            var outIndex = new int[ndim];
            int total = 1;
            for (int i = 0; i < ndim; i++)
            {
                total = checked(total * targetShape[i]);
            }

            var valuesIter = new BroadcastedIterator<T>(values, targetShape);
            for (int flat = 0; flat < total; flat++)
            {
                int destOffset = _offset;
                for (int d = 0; d < ndim; d++)
                {
                    int idxIntoSelf = d == normalizedAxis ? resolved[outIndex[d]] : outIndex[d];
                    destOffset += idxIntoSelf * _strides[d];
                }

                _data[destOffset] = valuesIter.Current;
                valuesIter.MoveNext();

                for (int d = ndim - 1; d >= 0; d--)
                {
                    outIndex[d]++;
                    if (outIndex[d] < targetShape[d])
                    {
                        break;
                    }

                    outIndex[d] = 0;
                }
            }
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
    }
}
