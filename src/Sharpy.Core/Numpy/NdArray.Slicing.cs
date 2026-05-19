using System;

namespace Sharpy
{
    /// <summary>
    /// Python-style slice descriptor: <c>start:stop:step</c>. Null components default to
    /// the natural endpoints (0/length for positive step, length-1/-1 for negative step).
    /// </summary>
    public readonly struct SliceSpec : IEquatable<SliceSpec>
    {
        /// <summary>Inclusive start index. <c>null</c> means "from the beginning".</summary>
        public int? Start { get; }

        /// <summary>Exclusive stop index. <c>null</c> means "to the end".</summary>
        public int? Stop { get; }

        /// <summary>Step between successive indices. <c>null</c> defaults to 1. Cannot be 0.</summary>
        public int? Step { get; }

        /// <summary>Construct a slice with the given (optional) start, stop, and step.</summary>
        public SliceSpec(int? start = null, int? stop = null, int? step = null)
        {
            if (step.HasValue && step.Value == 0)
            {
                throw new ArgumentException("slice step cannot be zero", nameof(step));
            }

            Start = start;
            Stop = stop;
            Step = step;
        }

        /// <summary>Sentinel slice representing <c>:</c> — take every element along this axis.</summary>
        public static SliceSpec All => new SliceSpec(null, null, null);

        /// <summary>Create a slice of the form <c>start:stop</c>.</summary>
        public static SliceSpec Range(int start, int stop) => new SliceSpec(start, stop, null);

        /// <summary>Create a slice of the form <c>start:stop:step</c>.</summary>
        public static SliceSpec Range(int start, int stop, int step) => new SliceSpec(start, stop, step);

        /// <summary>Resolve <see cref="Start"/>, <see cref="Stop"/>, and <see cref="Step"/> against the given axis length using Python <c>slice.indices()</c> semantics.</summary>
        internal (int start, int stop, int step, int length) Resolve(int axisLength)
        {
            int step = Step ?? 1;
            int defaultStart = step > 0 ? 0 : axisLength - 1;
            int defaultStop = step > 0 ? axisLength : -1;

            int start = NormalizeStart(Start, axisLength, step, defaultStart);
            int stop = NormalizeStop(Stop, axisLength, step, defaultStop);

            int length;
            if (step > 0)
            {
                length = System.Math.Max(0, (stop - start + step - 1) / step);
            }
            else
            {
                length = System.Math.Max(0, (start - stop - step - 1) / -step);
            }

            return (start, stop, step, length);
        }

        private static int NormalizeStart(int? value, int axisLength, int step, int @default)
        {
            if (!value.HasValue)
            {
                return @default;
            }

            int v = value.Value;
            if (v < 0)
            {
                v += axisLength;
            }

            if (step > 0)
            {
                if (v < 0)
                {
                    v = 0;
                }

                if (v > axisLength)
                {
                    v = axisLength;
                }
            }
            else
            {
                if (v < 0)
                {
                    v = -1;
                }

                if (v > axisLength - 1)
                {
                    v = axisLength - 1;
                }
            }

            return v;
        }

        private static int NormalizeStop(int? value, int axisLength, int step, int @default)
        {
            if (!value.HasValue)
            {
                return @default;
            }

            int v = value.Value;
            if (v < 0)
            {
                v += axisLength;
            }

            if (step > 0)
            {
                if (v < 0)
                {
                    v = 0;
                }

                if (v > axisLength)
                {
                    v = axisLength;
                }
            }
            else
            {
                if (v < 0)
                {
                    v = -1;
                }

                if (v > axisLength - 1)
                {
                    v = axisLength - 1;
                }
            }

            return v;
        }

        /// <inheritdoc />
        public bool Equals(SliceSpec other) =>
            Start == other.Start && Stop == other.Stop && Step == other.Step;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SliceSpec other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (Start?.GetHashCode() ?? 0);
                h = h * 31 + (Stop?.GetHashCode() ?? 0);
                h = h * 31 + (Step?.GetHashCode() ?? 0);
                return h;
            }
        }

        /// <summary>Equality operator.</summary>
        public static bool operator ==(SliceSpec left, SliceSpec right) => left.Equals(right);

        /// <summary>Inequality operator.</summary>
        public static bool operator !=(SliceSpec left, SliceSpec right) => !left.Equals(right);
    }

    public partial class NdArray<T>
    {
        /// <summary>
        /// Produce a zero-copy view defined by per-axis slice specs. The number of slices must
        /// equal <see cref="Ndim"/>. The view shares the underlying buffer with this array.
        /// </summary>
        /// <param name="slices">Per-axis slice descriptors. Length must equal <see cref="Ndim"/>.</param>
        /// <returns>A view of this array with the same <see cref="Ndim"/> but possibly smaller per-axis lengths.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="slices"/> is null.</exception>
        /// <exception cref="IndexError">Thrown when the slice count does not match <see cref="Ndim"/>.</exception>
        public NdArray<T> Slice(params SliceSpec[] slices)
        {
            if (slices == null)
            {
                throw new ArgumentNullException(nameof(slices));
            }

            if (slices.Length != _shape.Length)
            {
                throw new IndexError(
                    $"too many indices for array: array is {_shape.Length}-dimensional, but {slices.Length} slices were provided");
            }

            var newShape = new int[_shape.Length];
            var newStrides = new int[_shape.Length];
            int newOffset = _offset;

            for (int axis = 0; axis < slices.Length; axis++)
            {
                var (start, _, step, length) = slices[axis].Resolve(_shape[axis]);
                newShape[axis] = length;
                newStrides[axis] = _strides[axis] * step;
                newOffset += start * _strides[axis];
            }

            return new NdArray<T>(_data, newShape, newStrides, newOffset);
        }

        /// <summary>
        /// Return a 1-D view of row <paramref name="i"/> for a 2-D array. Negative indices follow
        /// Python semantics.
        /// </summary>
        /// <param name="i">Row index. Negative values count from the end.</param>
        /// <exception cref="InvalidOperationException">Thrown when this array is not 2-dimensional.</exception>
        /// <exception cref="IndexError">Thrown when <paramref name="i"/> is out of range.</exception>
        public NdArray<T> GetRow(int i)
        {
            if (_shape.Length != 2)
            {
                throw new InvalidOperationException(
                    $"GetRow requires a 2-D array, but this array is {_shape.Length}-D");
            }

            int rows = _shape[0];
            int actual = i < 0 ? i + rows : i;
            if (actual < 0 || actual >= rows)
            {
                throw new IndexError($"index {i} is out of bounds for axis 0 with size {rows}");
            }

            var rowShape = new[] { _shape[1] };
            var rowStrides = new[] { _strides[1] };
            int rowOffset = _offset + actual * _strides[0];
            return new NdArray<T>(_data, rowShape, rowStrides, rowOffset);
        }

        /// <summary>
        /// Return a 1-D view of column <paramref name="j"/> for a 2-D array. Negative indices follow
        /// Python semantics.
        /// </summary>
        /// <param name="j">Column index. Negative values count from the end.</param>
        /// <exception cref="InvalidOperationException">Thrown when this array is not 2-dimensional.</exception>
        /// <exception cref="IndexError">Thrown when <paramref name="j"/> is out of range.</exception>
        public NdArray<T> GetColumn(int j)
        {
            if (_shape.Length != 2)
            {
                throw new InvalidOperationException(
                    $"GetColumn requires a 2-D array, but this array is {_shape.Length}-D");
            }

            int cols = _shape[1];
            int actual = j < 0 ? j + cols : j;
            if (actual < 0 || actual >= cols)
            {
                throw new IndexError($"index {j} is out of bounds for axis 1 with size {cols}");
            }

            var colShape = new[] { _shape[0] };
            var colStrides = new[] { _strides[0] };
            int colOffset = _offset + actual * _strides[1];
            return new NdArray<T>(_data, colShape, colStrides, colOffset);
        }
    }
}
