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

    }
}
