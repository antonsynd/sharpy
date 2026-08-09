namespace Sharpy
{
    /// <summary>
    /// Operator overloads for Set&lt;T&gt;.
    /// Includes equality, subset/superset comparison, set operations, and truthiness operators.
    /// </summary>
    public sealed partial class Set<T>
    {
        #region Equality Operators

        /// <summary>
        /// Determines whether two sets are equal by comparing elements.
        /// </summary>
        /// <remarks>
        /// Returns true for both sets if they contain the same items,
        /// even if they are not the same set reference.
        /// </remarks>
        public static bool operator ==(Set<T>? left, Set<T>? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        /// <summary>
        /// Determines whether two sets are not equal.
        /// </summary>
        public static bool operator !=(Set<T>? left, Set<T>? right)
        {
            return !(left == right);
        }

        #endregion

        #region Subset/Superset Operators

        /// <summary>
        /// Determines whether the left set is a proper subset of the right set.
        /// </summary>
        public static bool operator <(Set<T> left, Set<T> right)
        {
            return left.IsProperSubset(right);
        }

        /// <summary>
        /// Determines whether the left set is a subset of the right set.
        /// </summary>
        public static bool operator <=(Set<T> left, Set<T> right)
        {
            return left.IsSubset(right);
        }

        /// <summary>
        /// Determines whether the left set is a proper superset of the right set.
        /// </summary>
        public static bool operator >(Set<T> left, Set<T> right)
        {
            return left.IsProperSuperset(right);
        }

        /// <summary>
        /// Determines whether the left set is a superset of the right set.
        /// </summary>
        public static bool operator >=(Set<T> left, Set<T> right)
        {
            return left.IsSuperset(right);
        }

        #endregion

        #region Set Operation Operators

        /// <summary>
        /// Returns a new set containing elements from both sets (union).
        /// </summary>
        public static Set<T> operator |(Set<T> left, Set<T> right)
        {
            return left.Union(right);
        }

        /// <summary>
        /// Returns a new set containing elements common to both sets (intersection).
        /// </summary>
        public static Set<T> operator &(Set<T> left, Set<T> right)
        {
            return left.Intersection(right);
        }

        /// <summary>
        /// Returns a new set containing elements in either set but not both (symmetric difference).
        /// </summary>
        public static Set<T> operator ^(Set<T> left, Set<T> right)
        {
            return left.SymmetricDifference(right);
        }

        /// <summary>
        /// Returns a new set containing elements in the left set but not in the right set (difference).
        /// </summary>
        public static Set<T> operator -(Set<T> left, Set<T> right)
        {
            return left.Difference(right);
        }

        #endregion

        #region Mixed set/frozenset operators (#1312)

        // CPython's left-operand rule: the LEFT operand's type decides the result, so `set | frozenset`
        // is a set and `frozenset | set` is a frozenset (verified with python3). Sharpy refused both
        // directions — SPY0222, "does not support operator '|'" — because each type declared its
        // operators only against itself.
        //
        // The mixed pairings live on the LEFT operand's type, one signature each: declaring
        // `Set<T> op FrozenSet<T>` on both types would be a duplicate C# would reject, and putting a
        // pairing on the right operand's type would put the result rule where the reader does not
        // look for it. The frozenset side of each pair is materialised into a Set<T> because Set's
        // set operations take Set<T>, not IEnumerable<T>.

        /// <summary>Union with a frozenset. The left operand decides: the result is a set.</summary>
        public static Set<T> operator |(Set<T> left, FrozenSet<T> right)
        {
            return left.Union(new Set<T>(right));
        }

        /// <summary>Intersection with a frozenset. The result is a set.</summary>
        public static Set<T> operator &(Set<T> left, FrozenSet<T> right)
        {
            return left.Intersection(new Set<T>(right));
        }

        /// <summary>Symmetric difference with a frozenset. The result is a set.</summary>
        public static Set<T> operator ^(Set<T> left, FrozenSet<T> right)
        {
            return left.SymmetricDifference(new Set<T>(right));
        }

        /// <summary>Difference with a frozenset. The result is a set.</summary>
        public static Set<T> operator -(Set<T> left, FrozenSet<T> right)
        {
            return left.Difference(new Set<T>(right));
        }

        /// <summary>Proper-subset comparison against a frozenset.</summary>
        public static bool operator <(Set<T> left, FrozenSet<T> right)
        {
            return left < new Set<T>(right);
        }

        /// <summary>Proper-superset comparison against a frozenset.</summary>
        public static bool operator >(Set<T> left, FrozenSet<T> right)
        {
            return left > new Set<T>(right);
        }

        /// <summary>Subset comparison against a frozenset.</summary>
        public static bool operator <=(Set<T> left, FrozenSet<T> right)
        {
            return left <= new Set<T>(right);
        }

        /// <summary>Superset comparison against a frozenset.</summary>
        public static bool operator >=(Set<T> left, FrozenSet<T> right)
        {
            return left >= new Set<T>(right);
        }

        // NO MIXED dict/frozendict PAIRINGS EITHER, same discipline, measured the same way. They
        // were written and then removed: `Dict | FrozenDict` gives Dict<K,V> a second `|` candidate,
        // which takes it off the compiler's single-candidate operator path, and the resolver then
        // fails to select — `dict | dict` itself stopped resolving. The mixed forms never worked in
        // the first place (the compiler does not find Dict's `|` through the path set/frozenset use),
        // so the pair was pure loss: an inert feature that broke a working one. Reinstating it needs
        // the compiler-side gap fixed first (#1361).

        // NO MIXED ==/!= , and the reason is measured. Declaring `operator ==(Set<T>?, FrozenSet<T>?)`
        // alongside the existing `operator ==(Set<T>?, Set<T>?)` makes `someSet == null` ambiguous
        // (CS9342) — the null literal converts to both. That broke Sharpy.Core's own DictKeyView on
        // the first build, and it would break the same shape in every user program, which is far
        // worse than the gap it closes. So `{1, 2} == frozenset([1, 2])`, True in CPython, stays
        // refused here; the four set operations and four comparisons above take non-nullable
        // operands and have no such conflict. Tracked as a deviation rather than papered over.

        #endregion

        #region Truthiness Operators

        /// <summary>
        /// Returns true if the set is not null and not empty.
        /// </summary>
        public static bool operator true(Set<T>? set)
        {
            return set is not null && set._set.Count > 0;
        }

        /// <summary>
        /// Returns true if the set is null or empty.
        /// </summary>
        public static bool operator false(Set<T>? set)
        {
            return set is null || set._set.Count == 0;
        }

        #endregion
    }
}
