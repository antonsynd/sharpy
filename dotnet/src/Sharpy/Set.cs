using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public sealed partial class Set<T> : Object, MutableSet<T>
    {
        private readonly HashSet<T> _set;

        public Set()
        {
            _set = [];
        }

        public Set(Collections.Interfaces.Set<T> set) : this()
        {
        }

        public int CompareTo(Collections.Interfaces.Set<T>? other)
        {
            throw new NotImplementedException();
        }

        public bool Contains(T x)
        {
            throw new NotImplementedException();
        }

        public bool __Contains__(T x)
        {
            throw new NotImplementedException();
        }

    }
}
