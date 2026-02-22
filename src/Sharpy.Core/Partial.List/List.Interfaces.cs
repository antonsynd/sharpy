using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// .NET interface implementations for List&lt;T&gt;.
    /// Implements IList&lt;T&gt;, IReadOnlyList&lt;T&gt;, ICollection&lt;T&gt;, IEnumerable&lt;T&gt;.
    /// </summary>
    public sealed partial class List<T>
    {
        #region ICollection<T>

        /// <summary>
        /// Gets the number of elements in the list.
        /// </summary>
        int ICollection<T>.Count => _list.Count;

        /// <summary>
        /// Gets a value indicating whether the list is read-only.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Removes the first occurrence of an item from the list.
        /// </summary>
        /// <returns>True if item was found and removed; otherwise false.</returns>
        bool ICollection<T>.Remove(T item) => _list.Remove(item);

        /// <summary>
        /// Copies the elements of the list to an array.
        /// </summary>
        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        #endregion

        #region IReadOnlyCollection<T>

        /// <summary>
        /// Gets the number of elements in the list.
        /// </summary>
        int IReadOnlyCollection<T>.Count => _list.Count;

        #endregion

        #region ISized

        /// <summary>
        /// Gets the number of elements in the list for len() dispatch.
        /// </summary>
        int ISized.Count => _list.Count;

        #endregion

        #region IList<T>

        /// <summary>
        /// Returns the zero-based index of the first occurrence of a value.
        /// </summary>
        /// <returns>The index of item if found; otherwise -1.</returns>
        int IList<T>.IndexOf(T item)
        {
            try
            {
                return (int)Index(item);
            }
            catch (ValueError)
            {
                return -1;
            }
        }

        /// <summary>
        /// Removes the element at the specified index.
        /// </summary>
        void IList<T>.RemoveAt(int index) => DeleteAt(index);

        /// <summary>
        /// Gets or sets the element at the specified index (explicit IList implementation).
        /// </summary>
        T IList<T>.this[int index]
        {
            get => this[index];
            set => this[index] = value;
        }

        #endregion

        #region IReadOnlyList<T>

        /// <summary>
        /// Gets the element at the specified index (explicit IReadOnlyList implementation).
        /// </summary>
        T IReadOnlyList<T>.this[int index] => this[index];

        #endregion

        #region IEnumerable<T>

        /// <summary>
        /// Returns an enumerator that iterates through the list.
        /// </summary>
        public IEnumerator<T> GetEnumerator() => new ListIterator<T>(this);

        /// <summary>
        /// Returns a non-generic enumerator for the IEnumerable interface.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region IEquatable<List<T>>

        /// <summary>
        /// Determines whether this list equals another list by comparing elements.
        /// </summary>
        public bool Equals(List<T>? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_list.Count != other._list.Count)
            {
                return false;
            }

            var comparer = EqualityComparer<T>.Default;

            for (int i = 0; i < _list.Count; ++i)
            {
                var leftElem = _list[i];
                var rightElem = other._list[i];

                if (!comparer.Equals(leftElem, rightElem))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether this list equals another object.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is List<T> other)
            {
                return Equals(other);
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for this list.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + typeof(List<T>).GetHashCode();
                hash = hash * 31 + _list.GetHashCode();
                return hash;
            }
        }

        #endregion
    }
}
