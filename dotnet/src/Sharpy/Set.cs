using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public sealed partial class Set<T> : Object, MutableSet<Set<T>, T>
    {
        private readonly HashSet<T> _set;

        public Set()
        {
            _set = [];
        }

        public Set(Set<T> set) : this()
        {
            _set.UnionWith(set._set);
        }

        public Set(IEnumerable<T> enumerable) : this()
        {
            if (enumerable is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            foreach (var x in enumerable)
            {
                _set.Add(x);
            }
        }

        public Set<T> Copy()
        {
            var newSet = new Set<T>();
            newSet._set.EnsureCapacity(_set.Count);
            newSet._set.UnionWith(_set);

            return newSet;
        }

        public HashSet<T> ToHashSet()
        {
            var result = new HashSet<T>();
            result.EnsureCapacity(_set.Count);
            result.UnionWith(_set);

            return result;
        }
    }
}
