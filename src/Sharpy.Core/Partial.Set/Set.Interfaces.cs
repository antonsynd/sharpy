using System.Collections;
using System.Collections.Generic;

namespace Sharpy.Core
{
    /// <summary>
    /// .NET interface implementations for Set&lt;T&gt;.
    /// Implements ISet&lt;T&gt;, ICollection&lt;T&gt;, IEnumerable&lt;T&gt;, IEquatable&lt;Set&lt;T&gt;&gt;.
    /// </summary>
    public sealed partial class Set<T>
    {
        #region ICollection<T>

        /// <summary>
        /// Gets the number of elements in the set.
        /// </summary>
        public int Count => _set.Count;

        /// <summary>
        /// Gets a value indicating whether the set is read-only.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Removes the specified element from the set.
        /// </summary>
        /// <returns>True if the element was found and removed; otherwise false.</returns>
        bool ICollection<T>.Remove(T item) => _set.Remove(item);

        /// <summary>
        /// Copies the elements of the set to an array.
        /// </summary>
        public void CopyTo(T[] array, int arrayIndex) => _set.CopyTo(array, arrayIndex);

        #endregion

        #region ISet<T>

        /// <summary>
        /// Adds an element to the set and returns whether the element was added.
        /// </summary>
        /// <remarks>
        /// Required for <see cref="ISet{T}"/>. The public <see cref="Add"/> method
        /// returns void (matching Python's set.add() behavior).
        /// </remarks>
        bool ISet<T>.Add(T item) => _set.Add(item);

        /// <summary>
        /// Removes all elements in the specified collection from the current set.
        /// </summary>
        public void ExceptWith(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.ExceptWith(other);
        }

        /// <summary>
        /// Modifies the current set to contain only elements present in both sets.
        /// </summary>
        public void IntersectWith(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.IntersectWith(other);
        }

        /// <summary>
        /// Determines whether the current set is a proper subset of the specified collection.
        /// </summary>
        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.IsProperSubsetOf(other);
        }

        /// <summary>
        /// Determines whether the current set is a proper superset of the specified collection.
        /// </summary>
        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.IsProperSupersetOf(other);
        }

        /// <summary>
        /// Determines whether the current set is a subset of the specified collection.
        /// </summary>
        public bool IsSubsetOf(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.IsSubsetOf(other);
        }

        /// <summary>
        /// Determines whether the current set is a superset of the specified collection.
        /// </summary>
        public bool IsSupersetOf(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.IsSupersetOf(other);
        }

        /// <summary>
        /// Determines whether the current set and a specified collection share common elements.
        /// </summary>
        public bool Overlaps(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.Overlaps(other);
        }

        /// <summary>
        /// Determines whether the current set and the specified collection contain the same elements.
        /// </summary>
        public bool SetEquals(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return _set.SetEquals(other);
        }

        /// <summary>
        /// Modifies the current set to contain only elements present in either the current set or the specified collection, but not both.
        /// </summary>
        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.SymmetricExceptWith(other);
        }

        /// <summary>
        /// Modifies the current set to contain all elements present in either the current set or the specified collection.
        /// </summary>
        public void UnionWith(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _set.UnionWith(other);
        }

        #endregion

        #region IEnumerable<T>

        /// <summary>
        /// Returns an enumerator that iterates through the set.
        /// </summary>
        public IEnumerator<T> GetEnumerator() => new SetIterator<T>(this);

        /// <summary>
        /// Returns a non-generic enumerator for the IEnumerable interface.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region IEquatable<Set<T>>

        /// <summary>
        /// Determines whether this set equals another set by comparing elements.
        /// </summary>
        public bool Equals(Set<T>? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return _set.SetEquals(other._set);
        }

        /// <summary>
        /// Determines whether this set is equal to the specified object.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is Set<T> set)
            {
                return Equals(set);
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for this set.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + typeof(Set<T>).GetHashCode();
                hash = hash * 31 + _set.GetHashCode();
                return hash;
            }
        }

        #endregion
    }
}
