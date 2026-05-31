using System;

namespace Sharpy
{
    /// <summary>
    /// N-dimensional array with NumPy-compatible boolean indexing operations.
    /// </summary>
    public partial class NdArray<T>
    {
        /// <summary>
        /// Return a 1-D copy containing the elements where <paramref name="mask"/> is true.
        /// </summary>
        /// <param name="mask">Boolean mask with the same shape as this array.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the mask shape does not match this array's shape.</exception>
        public NdArray<T> GetMasked(NdArray<bool> mask)
        {
            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            ValidateMaskShape(mask);

            // Two passes: count selected elements, then collect them.
            int count = 0;
            var maskIter = new BroadcastedIterator<bool>(mask, _shape);
            for (int i = 0; i < Size; i++)
            {
                if (maskIter.Current)
                {
                    count++;
                }

                maskIter.MoveNext();
            }

            var result = new T[count];
            int idx = 0;
            maskIter = new BroadcastedIterator<bool>(mask, _shape);
            var selfIter = new BroadcastedIterator<T>(this, _shape);
            for (int i = 0; i < Size; i++)
            {
                if (maskIter.Current)
                {
                    result[idx++] = selfIter.Current;
                }

                maskIter.MoveNext();
                selfIter.MoveNext();
            }

            return new NdArray<T>(result, new[] { count });
        }

        /// <summary>
        /// Assign <paramref name="value"/> to every position where <paramref name="mask"/> is true.
        /// </summary>
        /// <param name="mask">Boolean mask with the same shape as this array.</param>
        /// <param name="value">Scalar value written to each selected position.</param>
        public void SetMasked(NdArray<bool> mask, T value)
        {
            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            ValidateMaskShape(mask);

            int ndim = _shape.Length;
            var index = new int[ndim];
            var maskIter = new BroadcastedIterator<bool>(mask, _shape);

            for (int flat = 0; flat < Size; flat++)
            {
                if (maskIter.Current)
                {
                    int dataOffset = _offset;
                    for (int d = 0; d < ndim; d++)
                    {
                        dataOffset += index[d] * _strides[d];
                    }

                    _data[dataOffset] = value;
                }

                maskIter.MoveNext();

                // Increment row-major index.
                for (int d = ndim - 1; d >= 0; d--)
                {
                    index[d]++;
                    if (index[d] < _shape[d])
                    {
                        break;
                    }

                    index[d] = 0;
                }
            }
        }

        /// <summary>
        /// Assign values from <paramref name="values"/> to positions where <paramref name="mask"/> is true.
        /// <paramref name="values"/> must be 1-D with length equal to the number of true entries in the mask.
        /// </summary>
        public void SetMasked(NdArray<bool> mask, NdArray<T> values)
        {
            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            ValidateMaskShape(mask);

            if (values.Ndim != 1)
            {
                throw new ArgumentException(
                    $"values must be 1-D for boolean assignment, got {values.Ndim}-D",
                    nameof(values));
            }

            int ndim = _shape.Length;
            var index = new int[ndim];
            var maskIter = new BroadcastedIterator<bool>(mask, _shape);
            var valuesIter = new BroadcastedIterator<T>(values, values.Shape);
            int valuesConsumed = 0;

            for (int flat = 0; flat < Size; flat++)
            {
                if (maskIter.Current)
                {
                    if (valuesConsumed >= values.Size)
                    {
                        throw new ArgumentException(
                            $"NumPy boolean array indexing assignment cannot assign {values.Size} input values to {valuesConsumed + 1} output values where the mask is true",
                            nameof(values));
                    }

                    int dataOffset = _offset;
                    for (int d = 0; d < ndim; d++)
                    {
                        dataOffset += index[d] * _strides[d];
                    }

                    _data[dataOffset] = valuesIter.Current;
                    valuesIter.MoveNext();
                    valuesConsumed++;
                }

                maskIter.MoveNext();

                for (int d = ndim - 1; d >= 0; d--)
                {
                    index[d]++;
                    if (index[d] < _shape[d])
                    {
                        break;
                    }

                    index[d] = 0;
                }
            }
        }

        private void ValidateMaskShape(NdArray<bool> mask)
        {
            if (mask.Ndim != Ndim)
            {
                throw new ArgumentException(
                    $"boolean index did not match indexed array along dimension 0; mask has rank {mask.Ndim} but array has rank {Ndim}",
                    nameof(mask));
            }

            for (int d = 0; d < _shape.Length; d++)
            {
                if (mask.Shape[d] != _shape[d])
                {
                    throw new ArgumentException(
                        $"boolean index did not match indexed array along dimension {d}; size {mask.Shape[d]} != {_shape[d]}",
                        nameof(mask));
                }
            }
        }
    }
}
