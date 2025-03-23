using System.Text;

namespace Sharpy
{
    public sealed partial class List<T>
    {
        public override bool __Bool__()
        {
            return _list.Count > 0;
        }

        public override bool __Eq__(Object? obj)
        {
            if (obj is List<T> other)
            {
                return __Eq__(other);
            }

            return false;
        }

        public bool __Eq__(List<T>? other) {
            if (other is null) {
                return false;
            }

            if (_list.Count == other._list.Count)
            {
                for (uint i = 0; i < _list.Count; ++i)
                {
                    var leftElem = _list[(int)i];
                    var rightElem = other._list[(int)i];

                    if (!EqualityAdapterFactory<T>.AreEqual(leftElem, rightElem))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
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
