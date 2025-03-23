using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    /// <summary>
    /// A list of elements.
    /// </summary>
    public sealed partial class List<T> : Object, MutableSequence<List<T>, T>
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
            _list.AddRange(iterable);
        }

        /// <remarks>
        /// For collection initializers.
        /// </summary>
        public void Add(T item) => _list.Add(item);

        /// <summary>
        /// Return a shallow copy of the list.
        /// </summary>
        /// <returns></returns>
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
            Sort<T>(null, reverse);
        }

        public void Sort<TKey>(Func<T?, TKey?>? key = null, bool reverse = false)
        {
            // use https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.orderby?view=net-9.0&redirectedfrom=MSDN#System_Linq_Enumerable_OrderBy__2_System_Collections_Generic_IEnumerable___0__System_Func___0___1__
            _list.Sort(KeyComparerFactory<T, TKey>.Create(key));

            // TODO: Make this more efficient with the reverse
            if (reverse)
            {
                _list.Reverse();
            }
        }

        public List<T> __Add__(List<T> other)
        {
            var res = Copy();
            res.Extend(other);

            return res;
        }

        public List<T> __RAdd__(List<T> other)
        {
            var res = other.Copy();
            res.Extend(this);

            return res;
        }

        public List<T> __IAdd__(List<T> other) {
            Extend(other);

            return this;
        }

        public List<T> __Mul__(int i)
        {
            var res = new List<T>();

            if (i <= 0)
            {
                return res;
            }

            res._list.EnsureCapacity(_list.Count * i);

            for (; i > 0; --i)
            {
                res.Extend(this);
            }

            return res;
        }

        public List<T> __IMul__(int i) {
            if (i <= 0) {
                Clear();

                return this;
            }

            var originalLength = _list.Count;
            _list.EnsureCapacity(originalLength * i);

            for (; i > 0; --i)
            {
                for (uint j = 0; j < originalLength; ++j)
                {
                    _list.Add(_list[(int)j]);
                }
            }

            return this;
        }

        public List<T> __RMul__(int i) {
            return __Mul__(i);
        }

        /// <summary>
        /// Returns the item at the given index in the list.
        /// </summary>
        public T this[int index]
        {
            get
            {
                return __GetItem__(index);
            }
            set
            {
                __SetItem__(index, value);
            }
        }

        public List<T> this[int start, int end]
        {
            get
            {
                return this[start, end, 1];
            }
            set
            {
                this[start, end, 1] = value;
            }
        }

        public List<T> this[int start, int end, int step]
        {
            get
            {
                return __GetItem__(new Slice(start, end, step));
            }
            set
            {
                __SetItem__(new Slice(start, end, step), value);
            }
        }

        /// <remarks>
        /// This returns true for both lists if they contain the same elements,
        /// even if they are not the actual same list reference. Internally, it
        /// uses <see cref="object.Equals(object?)" /> for comparisons.
        /// </remarks>
        public static bool operator ==(List<T>? left, List<T>? right)
        {
            return left?.__Eq__(right) ?? false;
        }

        public static bool operator !=(List<T>? left, List<T>? right)
        {
            return !(left == right);
        }

        public static List<T> operator +(List<T> left, List<T> right)
        {
            return left.__Add__(right);
        }

        public static List<T> operator *(List<T> left, int i)
        {
            return left.__Mul__(i);
        }

        /// <summary>
        /// Creates a shallow copy this list as a .NET list.
        /// </summary>
        public System.Collections.Generic.List<T> ToList()
        {
            return [.. _list];
        }

        public static bool operator true(List<T> list) {
            return list.__Bool__();
        }

        public static bool operator false(List<T> list) {
            return !list.__Bool__();
        }
    }
}
