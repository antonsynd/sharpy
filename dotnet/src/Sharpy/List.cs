using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    /// <summary>
    /// A list of elements.
    /// </summary>
    public sealed partial class List<T> : Object, MutableSequence<List<T>, T>, IEquatable<List<T>>
    {
        private System.Collections.Generic.List<T> _list;

        /// <summary>
        /// Constructs an empty list.
        /// </summary>
        public List()
        {
            _list = [];
        }

        /// <summary>
        /// Constructs a list with a shallow copy of the elements in the
        /// iterable.
        /// </summary>
        public List(IEnumerable<T> iterable) : this()
        {
            if (iterable is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            _list.AddRange(iterable);
        }

        /// <remarks>
        /// For collection initializers.
        /// </summary>
        public void Add(T item) => _list.Add(item);

        /// <summary>
        /// Return a shallow copy of the list.
        /// </summary>
        public List<T> Copy()
        {
            var newList = new List<T>();
            newList._list.EnsureCapacity(_list.Count);
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

        public void Sort<TKey>(Func<T, TKey> key, bool reverse = false)
        {
            if (key is null)
            {
                throw new TypeError("Sort() key argument cannot be None");
            }

            // use https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.orderby?view=net-9.0&redirectedfrom=MSDN#System_Linq_Enumerable_OrderBy__2_System_Collections_Generic_IEnumerable___0__System_Func___0___1__
            _list.Sort(KeyComparerFactory<T, TKey>.Create(key));

            // TODO: Make this more efficient with the reverse
            if (reverse)
            {
                _list.Reverse();
            }
        }

        /// <summary>
        /// Creates a shallow copy this list as a .NET list.
        /// </summary>
        public System.Collections.Generic.List<T> ToList()
        {
            return [.. _list];
        }

    }
}
