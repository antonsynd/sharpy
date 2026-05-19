using System.Globalization;
using System.Text;

namespace Sharpy
{
    public partial class NdArray<T>
    {
        // Element-count threshold above which we truncate long axes when rendering.
        private const int TruncateThreshold = 1000;

        // When truncating, this many elements are shown at each end of each axis.
        private const int EdgeItems = 3;

        /// <summary>
        /// Format this array as a NumPy-style string: <c>array([1, 2, 3])</c> for 1-D arrays,
        /// <c>array([[1, 2], [3, 4]])</c> for 2-D arrays, and so on. Arrays with more than
        /// 1000 elements are truncated, showing the first and last 3 entries per axis.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("array(");

            bool truncate = Size > TruncateThreshold;

            if (_shape.Length == 0)
            {
                // 0-D scalar: array(value)
                if (Size == 1)
                {
                    sb.Append(FormatElement(_data[_offset]));
                }
                else
                {
                    sb.Append("[]");
                }
            }
            else
            {
                var index = new int[_shape.Length];
                AppendAxis(sb, index, 0, truncate);
            }

            sb.Append(')');
            return sb.ToString();
        }

        private void AppendAxis(StringBuilder sb, int[] index, int axis, bool truncate)
        {
            sb.Append('[');
            int dim = _shape[axis];

            if (truncate && dim > 2 * EdgeItems)
            {
                for (int i = 0; i < EdgeItems; i++)
                {
                    index[axis] = i;
                    AppendCell(sb, index, axis, truncate);
                    sb.Append(", ");
                }

                sb.Append("...");

                for (int i = dim - EdgeItems; i < dim; i++)
                {
                    sb.Append(", ");
                    index[axis] = i;
                    AppendCell(sb, index, axis, truncate);
                }
            }
            else
            {
                for (int i = 0; i < dim; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    index[axis] = i;
                    AppendCell(sb, index, axis, truncate);
                }
            }

            sb.Append(']');
        }

        private void AppendCell(StringBuilder sb, int[] index, int axis, bool truncate)
        {
            if (axis == _shape.Length - 1)
            {
                int offset = _offset;
                for (int a = 0; a < _shape.Length; a++)
                {
                    offset += index[a] * _strides[a];
                }

                sb.Append(FormatElement(_data[offset]));
            }
            else
            {
                AppendAxis(sb, index, axis + 1, truncate);
            }
        }

        private static string FormatElement(T value)
        {
            // Use invariant culture so the output is stable across locales.
            if (value is System.IFormattable fmt)
            {
                return fmt.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString() ?? string.Empty;
        }
    }
}
