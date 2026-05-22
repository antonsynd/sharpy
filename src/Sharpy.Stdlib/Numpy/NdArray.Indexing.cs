using System;

namespace Sharpy
{
    public partial class NdArray<T>
    {
        /// <summary>
        /// Get or set the element at the given N-D index. Negative indices follow Python semantics
        /// (e.g., <c>-1</c> refers to the last element along that axis).
        /// </summary>
        /// <param name="indices">One index per dimension. Length must equal <see cref="Ndim"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="indices"/> is null.</exception>
        /// <exception cref="IndexError">Thrown when the index count does not match <see cref="Ndim"/> or any index is out of range.</exception>
        public T this[params int[] indices]
        {
            get
            {
                int flatOffset = GetOffset(indices);
                return _data[flatOffset];
            }
            set
            {
                int flatOffset = GetOffset(indices);
                _data[flatOffset] = value;
            }
        }

        /// <summary>
        /// Translate an N-D index tuple to a flat offset into the underlying <c>_data</c> buffer.
        /// Negative indices are wrapped per Python semantics.
        /// </summary>
        internal int GetOffset(int[] indices)
        {
            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            if (indices.Length != _shape.Length)
            {
                throw new IndexError(
                    $"too many indices for array: array is {_shape.Length}-dimensional, but {indices.Length} were indexed");
            }

            int flat = _offset;
            for (int axis = 0; axis < indices.Length; axis++)
            {
                int idx = indices[axis];
                int dim = _shape[axis];

                if (idx < 0)
                {
                    idx += dim;
                }

                if (idx < 0 || idx >= dim)
                {
                    throw new IndexError(
                        $"index {indices[axis]} is out of bounds for axis {axis} with size {dim}");
                }

                flat += idx * _strides[axis];
            }

            return flat;
        }
    }
}
