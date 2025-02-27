using System.Text;

using static Sharpy.Builtins;

namespace Sharpy
{
    public sealed partial class List<T>
    {
        public override bool __Eq__(Object obj)
        {
            if (obj is List<T> other)
            {
                return this == other;
            }

            return false;
        }

        public override int __Hash__()
        {
            // Wrap overflows
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + typeof(List<T>).GetHashCode();
                hash = hash * 23 + _list.GetHashCode();

                return hash;
            }
        }

        public override string __Repr__()
        {
            var builder = new StringBuilder();
            builder.Append('[');

            foreach (var item in _list)
            {
                builder.Append(Repr(item));
            }

            builder.Append(']');

            return builder.ToString();
        }

    }
}
