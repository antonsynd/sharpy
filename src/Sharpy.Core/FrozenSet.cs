using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Sharpy
{
    /// <summary>
    /// An immutable, hashable set. Since frozenset is immutable, it can be used
    /// as a dictionary key or as an element of another set.
    /// </summary>
    /// <remarks>
    /// Backed by ImmutableHashSet{T} for .NET Standard 2.1 / Unity compatibility.
    /// Does NOT use System.Collections.Frozen (requires .NET 8+) or IReadOnlySet{T} (requires .NET 5+).
    /// </remarks>
    public sealed class FrozenSet<T> : IReadOnlyCollection<T>, IEquatable<FrozenSet<T>>
    {
        private readonly ImmutableHashSet<T> _set;

        /// <summary>Create an empty frozenset.</summary>
        public FrozenSet() => _set = ImmutableHashSet<T>.Empty;

        /// <summary>Create a frozenset from the given iterable.</summary>
        public FrozenSet(IEnumerable<T> items)
        {
            if (items is null)
                throw TypeError.IsNotInterface("NoneType", "iterable");
            _set = items.ToImmutableHashSet();
        }

        // Private constructor for internal operations
        private FrozenSet(ImmutableHashSet<T> set) => _set = set;

        /// <summary>Gets the number of elements in the frozenset.</summary>
        public int Count => _set.Count;
        /// <summary>Determines whether the frozenset contains the specified item.</summary>
        public bool Contains(T item) => _set.Contains(item);
        /// <summary>Returns an enumerator that iterates through the frozenset.</summary>
        public IEnumerator<T> GetEnumerator() => _set.GetEnumerator();
        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Determines whether this frozenset is a proper subset of the specified collection.</summary>
        public bool IsProperSubsetOf(IEnumerable<T> other) => _set.IsProperSubsetOf(other);
        /// <summary>Determines whether this frozenset is a proper superset of the specified collection.</summary>
        public bool IsProperSupersetOf(IEnumerable<T> other) => _set.IsProperSupersetOf(other);
        /// <summary>Determines whether this frozenset is a subset of the specified collection.</summary>
        public bool IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);
        /// <summary>Determines whether this frozenset is a superset of the specified collection.</summary>
        public bool IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);
        /// <summary>Determines whether this frozenset overlaps the specified collection.</summary>
        public bool Overlaps(IEnumerable<T> other) => _set.Overlaps(other);
        /// <summary>Determines whether this frozenset and the specified collection contain the same elements.</summary>
        public bool SetEquals(IEnumerable<T> other) => _set.SetEquals(other);

        /// <summary>Determines whether this frozenset is equal to the specified object.</summary>
        public override bool Equals(object? obj) => obj is FrozenSet<T> other && SetEquals(other);
        /// <summary>Determines whether this frozenset is equal to another frozenset.</summary>
        public bool Equals(FrozenSet<T>? other) => other is not null && _set.SetEquals(other._set);

        /// <summary>Returns an order-independent hash code for the frozenset.</summary>
        public override int GetHashCode()
        {
            // XOR of element hashes (order-independent, matches Python's frozenset)
            int hash = 0;
            foreach (var item in _set)
                hash ^= item?.GetHashCode() ?? 0;
            return hash;
        }

        /// <summary>Returns a string representation of the frozenset.</summary>
        public override string ToString() => Count == 0
            ? "frozenset()"
            : $"frozenset({{{string.Join(", ", _set.Select(x => Builtins.Repr(x)))}}})";

        /// <summary>Returns true if the frozenset is non-empty.</summary>
        public static bool operator true(FrozenSet<T>? s) => s is not null && s.Count > 0;
        /// <summary>Returns true if the frozenset is empty or null.</summary>
        public static bool operator false(FrozenSet<T>? s) => s is null || s.Count == 0;

        /// <summary>Returns the union of two frozensets.</summary>
        public static FrozenSet<T> operator |(FrozenSet<T> a, FrozenSet<T> b) =>
            new(a._set.Union(b._set));
        /// <summary>Returns the intersection of two frozensets.</summary>
        public static FrozenSet<T> operator &(FrozenSet<T> a, FrozenSet<T> b) =>
            new(a._set.Intersect(b._set));
        /// <summary>Returns the difference of two frozensets.</summary>
        public static FrozenSet<T> operator -(FrozenSet<T> a, FrozenSet<T> b) =>
            new(a._set.Except(b._set));
        /// <summary>Returns the symmetric difference of two frozensets.</summary>
        public static FrozenSet<T> operator ^(FrozenSet<T> a, FrozenSet<T> b) =>
            new(a._set.SymmetricExcept(b._set));

        /// <summary>Returns true if the left frozenset is a proper subset of the right.</summary>
        public static bool operator <(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsProperSubsetOf(b._set);
        /// <summary>Returns true if the left frozenset is a subset of the right.</summary>
        public static bool operator <=(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsSubsetOf(b._set);
        /// <summary>Returns true if the left frozenset is a proper superset of the right.</summary>
        public static bool operator >(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsProperSupersetOf(b._set);
        /// <summary>Returns true if the left frozenset is a superset of the right.</summary>
        public static bool operator >=(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsSupersetOf(b._set);
        /// <summary>Determines whether two frozensets are equal.</summary>
        public static bool operator ==(FrozenSet<T>? a, FrozenSet<T>? b) => a?.Equals(b) ?? b is null;
        /// <summary>Determines whether two frozensets are not equal.</summary>
        public static bool operator !=(FrozenSet<T>? a, FrozenSet<T>? b) => !(a == b);

        /// <summary>Return a shallow copy of the frozenset.</summary>
        public FrozenSet<T> Copy() => new(_set);
        /// <summary>Return a new frozenset with elements from this set and other.</summary>
        public FrozenSet<T> Union(FrozenSet<T> other) => this | other;
        /// <summary>Return a new frozenset with elements from this set and other.</summary>
        public FrozenSet<T> Union(IEnumerable<T> other) => new(_set.Union(other));
        /// <summary>Return a new frozenset with elements common to this set and other.</summary>
        public FrozenSet<T> Intersection(FrozenSet<T> other) => this & other;
        /// <summary>Return a new frozenset with elements common to this set and other.</summary>
        public FrozenSet<T> Intersection(IEnumerable<T> other) => new(_set.Intersect(other));
        /// <summary>Return a new frozenset with elements in this set but not in other.</summary>
        public FrozenSet<T> Difference(FrozenSet<T> other) => this - other;
        /// <summary>Return a new frozenset with elements in this set but not in other.</summary>
        public FrozenSet<T> Difference(IEnumerable<T> other) => new(_set.Except(other));
        /// <summary>Return a new frozenset with elements in either set but not both.</summary>
        public FrozenSet<T> SymmetricDifference(FrozenSet<T> other) => this ^ other;
        /// <summary>Return a new frozenset with elements in either set but not both.</summary>
        public FrozenSet<T> SymmetricDifference(IEnumerable<T> other) => new(_set.SymmetricExcept(other));
        /// <summary>Returns whether this frozenset is a subset of other.</summary>
        public bool IsSubset(FrozenSet<T> other) => this <= other;
        /// <summary>Returns whether this frozenset is a superset of other.</summary>
        public bool IsSuperset(FrozenSet<T> other) => this >= other;
        /// <summary>Returns whether this frozenset has no elements in common with other.</summary>
        public bool IsDisjoint(FrozenSet<T> other) => !_set.Overlaps(other._set);
        /// <summary>Returns whether this frozenset has no elements in common with other.</summary>
        public bool IsDisjoint(IEnumerable<T> other) => !_set.Overlaps(other);
    }
}
