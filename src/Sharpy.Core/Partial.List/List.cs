using System.Collections.Generic;
using System;
namespace Sharpy
{
    /// <summary>
    /// A mutable sequence of elements, similar to Python's <c>list</c>.
    /// Supports negative indexing, slicing, and Python-style methods.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list</typeparam>
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

        /// <summary>
        /// Implicitly converts a .NET array to a Sharpy list.
        /// </summary>
        public static implicit operator List<T>(T[] array)
        {
            return new List<T>(array);
        }

        /// <remarks>
        /// For collection initializers. Also a part of the
        /// System.Collections.Generic.ICollection interface.
        /// </remarks>
        public void Add(T item) => _list.Add(item);

        /// <summary>
        /// Return a shallow copy of the list.
        /// </summary>
        /// <returns>A new list with the same elements.</returns>
        /// <example>
        /// <code>
        /// x = [1, 2, 3]
        /// y = x.copy()    # [1, 2, 3]
        /// </code>
        /// </example>
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
        /// <param name="reverse">If <c>true</c>, sort in descending order.</param>
        /// <remarks>
        /// This is not a stable sort.
        /// </remarks>
        /// <example>
        /// <code>
        /// x = [3, 1, 2]
        /// x.sort()             # [1, 2, 3]
        /// x.sort(reverse=True) # [3, 2, 1]
        /// </code>
        /// </example>
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
