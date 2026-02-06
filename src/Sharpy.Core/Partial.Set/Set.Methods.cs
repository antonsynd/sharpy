using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharpy.Core
{
    using static Builtins;

    /// <summary>
    /// Python-style mutation methods and deprecated dunder methods for Set&lt;T&gt;.
    /// </summary>
    public sealed partial class Set<T>
    {
        #region Mutation Methods

        /// <summary>
        /// Add an element to the set (no effect if already present).
        /// </summary>
        /// <remarks>
        /// For initializer literals and part of
        /// System.Collections.Generic.ICollection interface.
        /// </remarks>
        public void Add(T x) => _set.Add(x);

        /// <summary>
        /// Remove an element from the set if present (no error if not present).
        /// </summary>
        public void Discard(T x) => _set.Remove(x);

        /// <summary>
        /// Remove all elements from the set.
        /// </summary>
        public void Clear() => _set.Clear();

        /// <summary>
        /// Remove and return an arbitrary element from the set.
        /// Raises KeyError if the set is empty.
        /// </summary>
        public T Pop()
        {
            if (_set.Count == 0)
            {
                throw new KeyError("pop from an empty set");
            }

            // Unsure about the efficiency of this
            var last = ((IEnumerable<T>)_set).Last();

            _set.Remove(last);

            return last;
        }

        /// <summary>
        /// Remove an element from the set.
        /// Raises KeyError if the element is not present.
        /// </summary>
        public void Remove(T x)
        {
            if (!_set.Remove(x))
            {
                throw new KeyError($"{x}");
            }
        }

        /// <summary>
        /// Returns whether the item is in the set.
        /// </summary>
        public bool Contains(T x) => _set.Contains(x);

        /// <summary>
        /// Returns whether this set has no elements in common with other.
        /// </summary>
        public bool IsDisjoint(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return !_set.Overlaps(other._set);
        }

        #endregion

        #region String Representation

        /// <summary>
        /// Returns a string representation of this set.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append('{');

            int i = 1;
            var numElems = _set.Count;

            foreach (var item in _set)
            {
                builder.Append(Repr(item));

                if (i < numElems)
                {
                    builder.Append(", ");
                }

                ++i;
            }

            builder.Append('}');

            return builder.ToString();
        }

        #endregion

        #region Deprecated Dunder Methods

        /// <summary>
        /// Deprecated: Use <see cref="Count"/> instead.
        /// </summary>
        public int __Len__() => Count;

        /// <summary>
        /// Deprecated: Use <see cref="Contains(T)"/> instead.
        /// </summary>
        public bool __Contains__(T x) => Contains(x);

        /// <summary>
        /// Deprecated: Use <see cref="GetEnumerator()"/> instead.
        /// </summary>
        public Iterator<T> __Iter__() => (Iterator<T>)GetEnumerator();

        /// <summary>
        /// Deprecated: Use <c>set</c> in a boolean context (operator true/false) instead.
        /// </summary>
        public bool __Bool__() => _set.Count > 0;

        /// <summary>
        /// Deprecated: Use <see cref="Equals(Set{T}?)"/> instead.
        /// </summary>
        public bool __Eq__(Set<T> other) => Equals(other);

        /// <summary>
        /// Deprecated: Use <see cref="Equals(object?)"/> instead.
        /// </summary>
        public bool __Eq__(object other) => Equals(other);

        /// <summary>
        /// Deprecated: Use <c>!Equals(other)</c> instead.
        /// </summary>
        public bool __Ne__(Set<T> other) => !Equals(other);

        /// <summary>
        /// Deprecated: Use <see cref="GetHashCode()"/> instead.
        /// </summary>
        public int __Hash__() => GetHashCode();

        /// <summary>
        /// Deprecated: Use <see cref="ToString()"/> instead.
        /// </summary>
        public string __Repr__() => ToString();

        /// <summary>
        /// Deprecated: Use <see cref="IsProperSubset(Set{T})"/> instead.
        /// </summary>
        public bool __Lt__(Set<T> other) => IsProperSubset(other);

        /// <summary>
        /// Deprecated: Use <see cref="IsSubset(Set{T})"/> instead.
        /// </summary>
        public bool __Le__(Set<T> other) => IsSubset(other);

        /// <summary>
        /// Deprecated: Use <see cref="IsProperSuperset(Set{T})"/> instead.
        /// </summary>
        public bool __Gt__(Set<T> other) => IsProperSuperset(other);

        /// <summary>
        /// Deprecated: Use <see cref="IsSuperset(Set{T})"/> instead.
        /// </summary>
        public bool __Ge__(Set<T> other) => IsSuperset(other);

        /// <summary>
        /// Deprecated: Use <see cref="Intersection(Set{T})"/> instead.
        /// </summary>
        public Set<T> __And__(Set<T> other) => Intersection(other);

        /// <summary>
        /// Deprecated: Use <see cref="Union(Set{T})"/> instead.
        /// </summary>
        public Set<T> __Or__(Set<T> other) => Union(other);

        /// <summary>
        /// Deprecated: Use <see cref="Difference(Set{T})"/> instead.
        /// </summary>
        public Set<T> __Sub__(Set<T> other) => Difference(other);

        /// <summary>
        /// Deprecated: Use <see cref="SymmetricDifference(Set{T})"/> instead.
        /// </summary>
        public Set<T> __XOr__(Set<T> other) => SymmetricDifference(other);

        /// <summary>
        /// Returns union with other set (reverse operand order for insertion).
        /// </summary>
        /// <remarks>
        /// For insertion order, the other set is iterated through first.
        /// Deprecated: Use <see cref="Union(Set{T})"/> with operands swapped instead.
        /// </remarks>
        public Set<T> __ROr__(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            var result = new Set<T>(other._set);

            foreach (var item in _set)
            {
                result._set.Add(item);
            }

            return result;
        }

        /// <summary>
        /// Returns difference with other as left operand.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>other.Difference(this)</c> instead.
        /// </remarks>
        public Set<T> __RSub__(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            var result = new Set<T>();

            foreach (var item in other._set)
            {
                if (!_set.Contains(item))
                {
                    result._set.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// In-place intersection.
        /// </summary>
        public void __IAnd__(Set<T> other) => _set.IntersectWith(other._set);

        /// <summary>
        /// In-place union.
        /// </summary>
        public void __IOr__(Set<T> other) => _set.UnionWith(other._set);

        /// <summary>
        /// In-place difference.
        /// </summary>
        public void __ISub__(Set<T> other) => _set.ExceptWith(other._set);

        /// <summary>
        /// In-place symmetric difference.
        /// </summary>
        public void __IXOr__(Set<T> other) => _set.SymmetricExceptWith(other._set);

        #endregion
    }
}
