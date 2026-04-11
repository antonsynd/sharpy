using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharpy
{
    using static Builtins;

    /// <summary>
    /// Python-style mutation methods for Set&lt;T&gt;.
    /// </summary>
    public sealed partial class Set<T>
    {
        #region Mutation Methods

        /// <summary>
        /// Add an element to the set (no effect if already present).
        /// </summary>
        /// <param name="x">The element to add.</param>
        /// <remarks>
        /// For initializer literals and part of
        /// System.Collections.Generic.ICollection interface.
        /// </remarks>
        /// <example>
        /// <code>
        /// s = {1, 2}
        /// s.add(3)    # {1, 2, 3}
        /// s.add(2)    # {1, 2, 3}  (no change)
        /// </code>
        /// </example>
        public void Add(T x) => _set.Add(x);

        /// <summary>
        /// Remove an element from the set if present (no error if not present).
        /// </summary>
        /// <param name="x">The element to discard.</param>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// s.discard(2)    # {1, 3}
        /// s.discard(9)    # {1, 3}  (no error)
        /// </code>
        /// </example>
        public void Discard(T x) => _set.Remove(x);

        /// <summary>
        /// Remove all elements from the set.
        /// </summary>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// s.clear()    # set()
        /// </code>
        /// </example>
        public void Clear() => _set.Clear();

        /// <summary>
        /// Remove and return an arbitrary element from the set.
        /// Raises KeyError if the set is empty.
        /// </summary>
        /// <returns>An arbitrary element from the set.</returns>
        /// <exception cref="KeyError">Thrown if the set is empty.</exception>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// s.pop()    # removes and returns an element
        /// </code>
        /// </example>
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
        /// <param name="x">The element to remove.</param>
        /// <exception cref="KeyError">Thrown if the element is not found.</exception>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// s.remove(2)    # {1, 3}
        /// </code>
        /// </example>
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
        /// <param name="x">The element to check for.</param>
        /// <returns><c>true</c> if the element is found; otherwise <c>false</c>.</returns>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// 2 in s    # True
        /// 5 in s    # False
        /// </code>
        /// </example>
        public bool Contains(T x) => _set.Contains(x);

        /// <summary>
        /// Returns whether this set has no elements in common with other.
        /// </summary>
        /// <param name="other">The set to test against.</param>
        /// <returns><c>true</c> if the sets have no common elements.</returns>
        /// <example>
        /// <code>
        /// a = {1, 2}
        /// b = {3, 4}
        /// a.isdisjoint(b)    # True
        /// </code>
        /// </example>
        public bool IsDisjoint(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return !_set.Overlaps(other._set);
        }

        /// <summary>
        /// Update the set, adding elements from the other set.
        /// </summary>
        /// <param name="other">The set of elements to add.</param>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// s.update({3, 4})    # {1, 2, 3, 4}
        /// </code>
        /// </example>
        public void Update(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.UnionWith(other._set);
        }

        /// <summary>
        /// Update the set, adding elements from the given iterable.
        /// </summary>
        /// <param name="other">The iterable of elements to add.</param>
        public void Update(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.UnionWith(other);
        }

        /// <summary>
        /// Update the set, removing elements found in the other set.
        /// </summary>
        /// <param name="other">The set of elements to remove.</param>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// s.difference_update({2})    # {1, 3}
        /// </code>
        /// </example>
        public void DifferenceUpdate(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.ExceptWith(other._set);
        }

        /// <summary>
        /// Update the set, removing elements found in the given iterable.
        /// </summary>
        /// <param name="other">The iterable of elements to remove.</param>
        public void DifferenceUpdate(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.ExceptWith(other);
        }

        /// <summary>
        /// Update the set, keeping only elements found in both sets.
        /// </summary>
        /// <param name="other">The set to intersect with.</param>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// s.intersection_update({2, 3, 4})    # {2, 3}
        /// </code>
        /// </example>
        public void IntersectionUpdate(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.IntersectWith(other._set);
        }

        /// <summary>
        /// Update the set, keeping only elements found in the given iterable.
        /// </summary>
        /// <param name="other">The iterable to intersect with.</param>
        public void IntersectionUpdate(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.IntersectWith(other);
        }

        /// <summary>
        /// Update the set, keeping only elements found in either set but not both.
        /// </summary>
        /// <param name="other">The set to compute symmetric difference with.</param>
        /// <example>
        /// <code>
        /// s = {1, 2, 3}
        /// s.symmetric_difference_update({2, 3, 4})    # {1, 4}
        /// </code>
        /// </example>
        public void SymmetricDifferenceUpdate(Set<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.SymmetricExceptWith(other._set);
        }

        /// <summary>
        /// Update the set, keeping only elements found in either set or the iterable but not both.
        /// </summary>
        /// <param name="other">The iterable to compute symmetric difference with.</param>
        public void SymmetricDifferenceUpdate(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.SymmetricExceptWith(other);
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

    }
}
