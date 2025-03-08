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
        public void Sort<TKey>(bool reverse = false, Func<T, TKey>? key = null)
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
            return __Len__() > 0;
        }

        public List<T> __Mul__(int i) {
            var res = new List<T>();

            if (i <= 0) {
                return res;
            }

            res._list.EnsureCapacity((int)__Len__() * i);

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

        public static bool operator ==(List<T> left, List<T> right)
        {
            return left._list == right._list;
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
            return _list[..(int)__Len__()];
        }

        private uint _NormalizeIndex(int i, bool forInsertion = false)
        {
            if (forInsertion)
            {
                if (i < 0) {
                    i = (int)__Len__() + i;
                }

                return (uint)Math.Clamp(i, 0, (int)__Len__());
            }

            if (i >= __Len__() || i < -__Len__())
            {
                throw new IndexError($"list index {i} out of range");
            }

            if (i < 0)
            {
                return (uint)((int)__Len__() + i);
            }

            return (uint)i;
        }

        private (uint, uint) _NormalizeSlice(int start, int end, bool forInsertion = false)
        {
            return (_NormalizeIndex(start, forInsertion), _NormalizeIndex(end, forInsertion));
        }
    }
}
