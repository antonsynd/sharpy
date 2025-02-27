using System.Text;
using System.Linq;
using System.Reflection;
using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    /// <summary>
    /// A list of elements.
    /// </summary>
    public sealed partial class List<T> : Object, MutableSequence<T> where T : IEquatable<T>
    {
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

        /// <summary>
        /// Returns a reference to the underlying list.
        /// </summary>
        public unsafe System.Collections.Generic.List<T> AsList()
        {
            return _list;
        }

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

        /// <summary>
        /// Creates a shallow copy this list as a .NET list.
        /// </summary>
        public System.Collections.Generic.List<T> ToList()
        {
            return _list[..(int)__Len__()];
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
                // TODO
            }
        }

        public List<T> this[int start, int end, int step = 1]
        {
            get
            {
                if (step == 0)
                {
                    throw new ValueError("slice step cannot be zero");
                }

                if (step < 0)
                {

                    return new List<T>();
                }

                (start, end) = ((int, int))_NormalizeSlice(start, end);

                return new List<T>
                {
                    _list = [.. _list.Skip(start).Take(end - start).Where((item, index) => index % step == 0)]
                };
            }
            set
            {
                // TODO
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

        private uint _NormalizeIndex(int i, bool forInsertion = false)
        {
            if (forInsertion)
            {
                return (uint)Math.Clamp(i, 0, (int)__Len__());
            }

            if (i > __Len__() || i < -__Len__())
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

        private System.Collections.Generic.List<T> _list;
    }
}
