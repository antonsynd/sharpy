using System.Collections.Immutable;

namespace Sharpy.Core;

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

    public FrozenSet() => _set = ImmutableHashSet<T>.Empty;

    public FrozenSet(IEnumerable<T> items)
    {
        if (items is null)
            throw TypeError.IsNotInterface("NoneType", "iterable");
        _set = items.ToImmutableHashSet();
    }

    // Private constructor for internal operations
    private FrozenSet(ImmutableHashSet<T> set) => _set = set;

    // IReadOnlyCollection<T> implementation
    public int Count => _set.Count;
    public bool Contains(T item) => _set.Contains(item);
    public IEnumerator<T> GetEnumerator() => _set.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    // Set query methods (equivalent to IReadOnlySet<T> which isn't available in .NET Standard 2.1)
    public bool IsProperSubsetOf(IEnumerable<T> other) => _set.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => _set.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<T> other) => _set.Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => _set.SetEquals(other);

    // System.Object overrides
    public override bool Equals(object? obj) => obj is FrozenSet<T> other && SetEquals(other);
    public bool Equals(FrozenSet<T>? other) => other is not null && _set.SetEquals(other._set);

    public override int GetHashCode()
    {
        // XOR of element hashes (order-independent, matches Python's frozenset)
        int hash = 0;
        foreach (var item in _set)
            hash ^= item?.GetHashCode() ?? 0;
        return hash;
    }

    public override string ToString() => Count == 0
        ? "frozenset()"
        : $"frozenset({{{string.Join(", ", _set.Select(x => Exports.Repr(x)))}}})";

    // Truthiness operators
    public static bool operator true(FrozenSet<T>? s) => s is not null && s.Count > 0;
    public static bool operator false(FrozenSet<T>? s) => s is null || s.Count == 0;

    // Set operators - return new FrozenSet instances
    public static FrozenSet<T> operator |(FrozenSet<T> a, FrozenSet<T> b) =>
        new(a._set.Union(b._set));
    public static FrozenSet<T> operator &(FrozenSet<T> a, FrozenSet<T> b) =>
        new(a._set.Intersect(b._set));
    public static FrozenSet<T> operator -(FrozenSet<T> a, FrozenSet<T> b) =>
        new(a._set.Except(b._set));
    public static FrozenSet<T> operator ^(FrozenSet<T> a, FrozenSet<T> b) =>
        new(a._set.SymmetricExcept(b._set));

    // Comparison operators (subset/superset)
    public static bool operator <(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsProperSubsetOf(b._set);
    public static bool operator <=(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsSubsetOf(b._set);
    public static bool operator >(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsProperSupersetOf(b._set);
    public static bool operator >=(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsSupersetOf(b._set);
    public static bool operator ==(FrozenSet<T>? a, FrozenSet<T>? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(FrozenSet<T>? a, FrozenSet<T>? b) => !(a == b);

    // Python-style methods
    public FrozenSet<T> Copy() => new(_set);
    public FrozenSet<T> Union(FrozenSet<T> other) => this | other;
    public FrozenSet<T> Union(IEnumerable<T> other) => new(_set.Union(other));
    public FrozenSet<T> Intersection(FrozenSet<T> other) => this & other;
    public FrozenSet<T> Intersection(IEnumerable<T> other) => new(_set.Intersect(other));
    public FrozenSet<T> Difference(FrozenSet<T> other) => this - other;
    public FrozenSet<T> Difference(IEnumerable<T> other) => new(_set.Except(other));
    public FrozenSet<T> SymmetricDifference(FrozenSet<T> other) => this ^ other;
    public FrozenSet<T> SymmetricDifference(IEnumerable<T> other) => new(_set.SymmetricExcept(other));
    public bool IsSubset(FrozenSet<T> other) => this <= other;
    public bool IsSuperset(FrozenSet<T> other) => this >= other;
    public bool IsDisjoint(FrozenSet<T> other) => !_set.Overlaps(other._set);
    public bool IsDisjoint(IEnumerable<T> other) => !_set.Overlaps(other);
}
