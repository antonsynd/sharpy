using System;
using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Interface implementations for <see cref="NdArray{T}"/> — <c>IEnumerable&lt;T&gt;</c>,
    /// <c>ISized</c>, structural equality, and conversion helpers.
    /// </summary>
    public partial class NdArray<T> : IEnumerable<T>, ISized
    {
        /// <summary>
        /// <c>len(arr)</c>-equivalent: the length of the first axis for non-scalar arrays.
        /// For 0-D scalars this returns 1 (matches the underlying buffer size).
        /// </summary>
        /// <remarks>
        /// Python's <c>len(ndarray)</c> returns the length of the first axis and raises
        /// <c>TypeError</c> on 0-D arrays. We return <see cref="Size"/> for 0-D for consistency
        /// with other Sharpy collections; users of higher-rank arrays get the expected first-axis length.
        /// </remarks>
        public int Count => _shape.Length == 0 ? Size : _shape[0];

        /// <summary>
        /// Iterate over the flattened elements in row-major order. Mirrors NumPy's iteration
        /// behavior on 1-D arrays and gives a predictable flat traversal for higher ranks.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            if (Size == 0)
            {
                yield break;
            }

            int ndim = _shape.Length;
            if (ndim == 0)
            {
                yield return _data[_offset];
                yield break;
            }

            var index = new int[ndim];
            int flatOffset = _offset;

            for (int flat = 0; flat < Size; flat++)
            {
                yield return _data[flatOffset];

                // Increment N-D index in row-major order and update flatOffset incrementally.
                bool wrapped;
                int axis = ndim - 1;
                do
                {
                    index[axis]++;
                    flatOffset += _strides[axis];
                    wrapped = false;

                    if (index[axis] >= _shape[axis])
                    {
                        flatOffset -= _strides[axis] * _shape[axis];
                        index[axis] = 0;
                        wrapped = true;
                        axis--;
                    }
                }
                while (wrapped && axis >= 0);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Structural equality: two arrays are equal when they have the same shape and every
        /// corresponding element is equal under <see cref="IEquatable{T}"/>.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is NdArray<T> other)
            {
                return ShapeEquals(other) && ElementsEqual(other);
            }

            return false;
        }

        /// <summary>
        /// Hash code derived from shape and a sample of elements. Designed to be cheap rather
        /// than uniformly distributed across large arrays.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < _shape.Length; i++)
                {
                    h = h * 31 + _shape[i];
                }

                // Sample the first, middle, and last elements (if any) to bias the hash by content.
                if (Size > 0)
                {
                    h = h * 31 + this.At(0).GetHashCode();
                    h = h * 31 + this.At(Size / 2).GetHashCode();
                    h = h * 31 + this.At(Size - 1).GetHashCode();
                }

                return h;
            }
        }

        /// <summary>
        /// Convert this array to a nested <c>List&lt;...&gt;</c> mirror — the equivalent of NumPy's
        /// <c>ndarray.tolist()</c>. The result type depends on rank: 1-D → <c>List&lt;T&gt;</c>,
        /// 2-D → <c>List&lt;List&lt;T&gt;&gt;</c>, etc. Returned as <c>object</c> because the static
        /// nesting depth depends on the runtime rank.
        /// </summary>
        public object Tolist()
        {
            if (_shape.Length == 0)
            {
                return _data[_offset]!;
            }

            return ToListRecursive(0, _offset);
        }

        // -- Internal helpers -------------------------------------------------------

        // Visit the flat element at a given linear position (0..Size-1) through the strided view.
        private T At(int flat)
        {
            int ndim = _shape.Length;
            if (ndim == 0)
            {
                return _data[_offset];
            }

            int offset = _offset;
            int rem = flat;
            for (int d = ndim - 1; d >= 0; d--)
            {
                int dim = _shape[d];
                int idx = dim == 0 ? 0 : rem % dim;
                rem = dim == 0 ? 0 : rem / dim;
                offset += idx * _strides[d];
            }

            return _data[offset];
        }

        private bool ShapeEquals(NdArray<T> other)
        {
            if (_shape.Length != other._shape.Length)
            {
                return false;
            }

            for (int i = 0; i < _shape.Length; i++)
            {
                if (_shape[i] != other._shape[i])
                {
                    return false;
                }
            }

            return true;
        }

        private bool ElementsEqual(NdArray<T> other)
        {
            if (Size != other.Size)
            {
                return false;
            }

            var thisEnum = GetEnumerator();
            var otherEnum = other.GetEnumerator();

            while (thisEnum.MoveNext() && otherEnum.MoveNext())
            {
                if (!thisEnum.Current.Equals(otherEnum.Current))
                {
                    return false;
                }
            }

            return true;
        }

        // Build a nested List<...> mirroring the rank. At the innermost axis we produce List<T>.
        private object ToListRecursive(int axis, int baseOffset)
        {
            int dim = _shape[axis];

            if (axis == _shape.Length - 1)
            {
                var leaf = new System.Collections.Generic.List<T>(dim);
                for (int i = 0; i < dim; i++)
                {
                    leaf.Add(_data[baseOffset + i * _strides[axis]]);
                }

                return leaf;
            }

            var inner = new System.Collections.Generic.List<object>(dim);
            for (int i = 0; i < dim; i++)
            {
                inner.Add(ToListRecursive(axis + 1, baseOffset + i * _strides[axis]));
            }

            return inner;
        }
    }
}
