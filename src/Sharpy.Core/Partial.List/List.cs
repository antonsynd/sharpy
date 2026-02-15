using System.Collections.Generic;
using System;
namespace Sharpy
{
    /// <summary>
    /// A list of elements.
    /// </summary>
    public sealed partial class List<T>
        : IList<T>,
          IReadOnlyList<T>,
          System.IEquatable<List<T>>,
          ISized
    {
        private System.Collections.Generic.List<T> _list;

        /// <summary>
        /// Constructs an empty list.
        /// </summary>
        public List()
        {
            _list = new System.Collections.Generic.List<T>();
        }

        /// <summary>
        /// Constructs a list with a shallow copy of the elements in the
        /// iterable.
        /// </summary>
        public List(IEnumerable<T> enumerable) : this()
        {
            if (enumerable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _list.AddRange(enumerable);
        }

        /// <remarks>
        /// For collection initializers. Also a part of the
        /// System.Collections.Generic.ICollection interface.
        /// </remarks>
        public void Add(T item) => _list.Add(item);

        /// <summary>
        /// Return a shallow copy of the list.
        /// </summary>
        public List<T> Copy()
        {
            var newList = new List<T>();
            newList._list.AddRange(_list);

            return newList;
        }

        /// <summary>
        /// Sort the items of the list in place (the arguments can be used for
        /// sort customization, see Sorted() for their explanation).
        /// </summary>
        /// <remarks>
        /// This is not a stable sort.
        /// </remarks>
        public void Sort(bool reverse = false)
        {
            Sort(value => value, reverse);
        }

        /// <summary>
        /// Sort the items of the list in place (the arguments can be used for
        /// sort customization, see Sorted() for their explanation).
        /// </summary>
        /// <remarks>
        /// This is not a stable sort.
        /// </remarks>
        public void Sort<TKey>(Func<T, TKey> key, bool reverse = false)
        {
            if (key is null)
            {
                throw TypeError.ArgNone("sort", "key");
            }

            var comp = KeyComparerFactory<T, TKey>.Create(key);

            if (reverse)
            {
                _list.Sort(Comparer<T>.Create((a, b) => comp.Compare(b, a)));
            }
            else
            {
                _list.Sort(comp);
            }
        }

        /// <summary>
        /// Creates a shallow copy this list as a .NET list.
        /// </summary>
        public System.Collections.Generic.List<T> ToList()
        {
            return new System.Collections.Generic.List<T>(_list);
        }
    }
}
