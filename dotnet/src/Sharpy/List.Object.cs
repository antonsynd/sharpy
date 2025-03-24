using System.Text;

namespace Sharpy
{
    public sealed partial class List<T>
    {
        public override bool __Bool__()
        {
            return _list.Count > 0;
        }

        public override int __Hash__()
        {
            var hashCode = new HashCode();
            hashCode.Add(typeof(List<T>).GetHashCode());
            hashCode.Add(_list.GetHashCode());

            return hashCode.ToHashCode();
        }

        public override string __Repr__()
        {
            var builder = new StringBuilder();
            builder.Append('[');

            uint i = 1;
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
    }
}
