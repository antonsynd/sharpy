using System;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Shape-broadcasting utilities — implements NumPy's broadcasting rules.
    /// </summary>
    /// <remarks>
    /// Two shapes are broadcastable when, aligned right-to-left, each pair of dimensions is
    /// either equal, or one of them is 1. The broadcast shape takes the maximum along each
    /// aligned axis. Missing leading dimensions on the shorter shape are treated as 1.
    /// </remarks>
    internal static class Broadcasting
    {
        /// <summary>
        /// Compute the broadcast shape of two input shapes per NumPy rules.
        /// </summary>
        /// <param name="shapeA">First input shape.</param>
        /// <param name="shapeB">Second input shape.</param>
        /// <returns>The broadcast shape — a new array.</returns>
        /// <exception cref="ArgumentException">Thrown when the shapes are not broadcastable.</exception>
        public static int[] BroadcastShapes(int[] shapeA, int[] shapeB)
        {
            if (shapeA == null)
            {
                throw new ArgumentNullException(nameof(shapeA));
            }

            if (shapeB == null)
            {
                throw new ArgumentNullException(nameof(shapeB));
            }

            int rank = System.Math.Max(shapeA.Length, shapeB.Length);
            var result = new int[rank];

            for (int i = 0; i < rank; i++)
            {
                int axisA = i < shapeA.Length ? shapeA[shapeA.Length - 1 - i] : 1;
                int axisB = i < shapeB.Length ? shapeB[shapeB.Length - 1 - i] : 1;

                int dim;
                if (axisA == axisB)
                {
                    dim = axisA;
                }
                else if (axisA == 1)
                {
                    dim = axisB;
                }
                else if (axisB == 1)
                {
                    dim = axisA;
                }
                else
                {
                    throw new ArgumentException(
                        $"shapes {FormatShape(shapeA)} and {FormatShape(shapeB)} are not broadcastable");
                }

                result[rank - 1 - i] = dim;
            }

            return result;
        }

        /// <summary>
        /// Produce a stride vector that maps <paramref name="targetShape"/> indices back into
        /// <paramref name="sourceShape"/>, treating broadcast (size-1) axes as zero-stride.
        /// </summary>
        public static int[] BroadcastStrides(int[] sourceShape, int[] sourceStrides, int[] targetShape)
        {
            if (sourceShape == null)
            {
                throw new ArgumentNullException(nameof(sourceShape));
            }

            if (sourceStrides == null)
            {
                throw new ArgumentNullException(nameof(sourceStrides));
            }

            if (targetShape == null)
            {
                throw new ArgumentNullException(nameof(targetShape));
            }

            int rank = targetShape.Length;
            var strides = new int[rank];
            int sourceRank = sourceShape.Length;

            for (int i = 0; i < rank; i++)
            {
                // Right-aligned axis index from the right.
                int rIndex = rank - 1 - i;
                int sIndex = sourceRank - 1 - i;

                if (sIndex < 0)
                {
                    // Source has fewer dims — treat missing leading axes as size 1 (stride 0).
                    strides[rIndex] = 0;
                }
                else if (sourceShape[sIndex] == 1 && targetShape[rIndex] != 1)
                {
                    strides[rIndex] = 0;
                }
                else if (sourceShape[sIndex] == targetShape[rIndex])
                {
                    strides[rIndex] = sourceStrides[sIndex];
                }
                else
                {
                    throw new ArgumentException(
                        $"source shape {FormatShape(sourceShape)} not broadcastable to {FormatShape(targetShape)}");
                }
            }

            return strides;
        }

        private static string FormatShape(int[] shape)
        {
            var sb = new StringBuilder();
            sb.Append('(');
            for (int i = 0; i < shape.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(shape[i]);
            }

            if (shape.Length == 1)
            {
                sb.Append(',');
            }

            sb.Append(')');
            return sb.ToString();
        }
    }

    /// <summary>
    /// Iterator that walks a target shape, providing the corresponding flat offset into a
    /// source <see cref="NdArray{T}"/> as if it had been broadcast to <c>targetShape</c>.
    /// </summary>
    /// <typeparam name="T">Element type of the source array.</typeparam>
    /// <remarks>
    /// Use this when implementing elementwise binary operations to avoid materializing a
    /// broadcast copy of the smaller operand.
    /// </remarks>
    internal sealed class BroadcastedIterator<T> where T : struct, IEquatable<T>
    {
        private readonly NdArray<T> _source;
        private readonly int[] _strides;
        private readonly int[] _targetShape;
        private readonly int[] _index;
        private int _flatOffset;
        private int _position;
        private readonly int _total;

        /// <summary>Construct an iterator over <paramref name="source"/> broadcast to <paramref name="targetShape"/>.</summary>
        public BroadcastedIterator(NdArray<T> source, int[] targetShape)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (targetShape == null)
            {
                throw new ArgumentNullException(nameof(targetShape));
            }

            _source = source;
            _targetShape = targetShape;
            _strides = Broadcasting.BroadcastStrides(source._shape, source._strides, targetShape);
            _index = new int[targetShape.Length];
            _flatOffset = source._offset;
            _position = 0;

            int total = 1;
            for (int i = 0; i < targetShape.Length; i++)
            {
                total = checked(total * targetShape[i]);
            }

            _total = total;
        }

        /// <summary>Total number of elements that will be produced.</summary>
        public int Total => _total;

        /// <summary>True once <see cref="Total"/> elements have been visited.</summary>
        public bool IsDone => _position >= _total;

        /// <summary>Current element value at the current logical position.</summary>
        public T Current => _source._data[_flatOffset];

        /// <summary>Advance the iterator by one logical position in row-major order.</summary>
        public void MoveNext()
        {
            _position++;
            if (_position >= _total)
            {
                return;
            }

            // Increment N-D index and update flatOffset incrementally.
            for (int axis = _targetShape.Length - 1; axis >= 0; axis--)
            {
                _index[axis]++;
                _flatOffset += _strides[axis];

                if (_index[axis] < _targetShape[axis])
                {
                    return;
                }

                // Carry: wrap this axis back to 0 and subtract stride * dim from offset.
                _flatOffset -= _strides[axis] * _targetShape[axis];
                _index[axis] = 0;
            }
        }
    }
}
