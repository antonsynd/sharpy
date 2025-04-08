using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public sealed partial class Set<T> : Object, IMutableSet<Set<T>, T>, IComparable<Set<T>>
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

        public bool IsSubset(Set<T> other)
        {
            return __Le__(other);
        }

        public bool IsSuperset(Set<T> other)
        {
            return __Ge__(other);
        }

        public Set<T> Union(Set<T> other)
        {
            return __Or__(other);
        }

        public Set<T> Intersection(Set<T> other)
        {
            return __And__(other);
        }

        public Set<T> Difference(Set<T> other)
        {
            return __Sub__(other);
        }

        public Set<T> SymmetricDifference(Set<T> other)
        {
            return __XOr__(other);
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
