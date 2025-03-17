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
        public void Sort<TKey>(Func<T, TKey>? key = null, bool reverse = false)
        {
            if (key == null)
            {
                // use https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.orderby?view=net-9.0&redirectedfrom=MSDN#System_Linq_Enumerable_OrderBy__2_System_Collections_Generic_IEnumerable___0__System_Func___0___1__
                _list.Sort();
            }
            else
            {
                _list.Sort(Comparer<T>.Create((a, b) => Comparer<TKey>.Default.Compare(key(a), key(b))));
            }

            // TODO: Make this more efficient with the reverse
            if (reverse)
            {
                _list.Reverse();
            }
        }

        public List<T> __Add__(List<T> other) {
            var res = Copy();
            res.Extend(other);

            return res;
        }

        public override bool __Bool__() {
            return _list.Count > 0;
        }

        public List<T> __Mul__(int i) {
            var res = new List<T>();

            if (i <= 0) {
                return res;
            }

            res._list.EnsureCapacity(_list.Count * i);

            for (; i > 0; --i) {
                res.Extend(this);
            }

            return res;
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
        public static bool operator ==(List<T> left, List<T> right)
        {
            if (left._list.Count == right._list.Count) {
                for (uint i = 0; i < left._list.Count; ++i) {
                    var leftElem = left._list[(int)i];

                    if (leftElem is null) {
                        if (right._list[(int)i] is not null) {
                            return false;
                        }
                    } else if (!leftElem.Equals(right._list[(int)i])) {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public static bool operator !=(List<T> left, List<T> right)
        {
            return !(left == right);
        }

        public static List<T> operator +(List<T> left, List<T> right) {
            return left.__Add__(right);
        }

        public static List<T> operator *(List<T> left, int i) {
            return left.__Mul__(i);
        }

        /// <summary>
        /// Creates a shallow copy this list as a .NET list.
        /// </summary>
        public System.Collections.Generic.List<T> ToList()
        {
            return [.._list];
        }

        private uint _NormalizeIndex(int i, bool forSlice, bool forInsertion)
        {
            if (forSlice || forInsertion)
            {
                if (i < 0) {
                    i = _list.Count + i;
                }

                return (uint)Math.Clamp(i, 0, _list.Count);
            }

            if (i >= _list.Count || i < -_list.Count)
            {
                throw new IndexError($"list index {i} out of range");
            }

            if (i < 0)
            {
                return (uint)(_list.Count + i);
            }

            return (uint)i;
        }

        private (uint, uint) _NormalizeSlice(int start, int end)
        {
            return (_NormalizeIndex(start, true, false), _NormalizeIndex(end, true, false));
        }
    }
}
