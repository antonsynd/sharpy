using System.Text;

namespace Sharpy.Core
{
    using static Sharpy.Core.Exports;

    public sealed partial class List<T>
    {
        /// <summary>
        /// Returns a string representation of this list.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append('[');

            int i = 1;
            var numElems = _list.Count;

            foreach (var item in _list)
            {
                builder.Append(Repr(item));

                if (i < numElems)
                {
                    builder.Append(", ");
                }

                ++i;
            }

            builder.Append(']');

            return builder.ToString();
        }

        /// <summary>
        /// Deprecated: Use <see cref="ToString()"/> instead.
        /// </summary>
        public string __Repr__()
        {
            return ToString();
        }
    }
}
