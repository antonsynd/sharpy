using System.Collections.Generic;
namespace Sharpy.Core
{
    /// <summary>
    /// Implements .NET's <see cref="ISet{T}"/> interface.
    /// </summary>
    public sealed partial class Set<T>
    {
        /// <summary>
        /// Adds an element to the set and returns whether the element was added.
        /// </summary>
        /// <remarks>
        /// Required for <see cref="ISet{T}"/>. The existing <see cref="Add"/> method
        /// returns void (matching Python's set.add() behavior).
        /// </remarks>
        bool System.Collections.Generic.ISet<T>.Add(T item)
        {
            return _set.Add(item);
        }

        // ICollection<T>.Count is now satisfied by the public Count property in Set.ISized.cs

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
    }
}
