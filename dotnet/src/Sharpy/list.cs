#nullable enable

using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// A list of elements.
    /// </summary>
    public sealed class List<T> : Object, ISequence<T> where T : IEquatable<T>
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
        /// Add an item to the end of the list. Similar to a[len(a):] = [x].
        /// </summary>
        public void Append(T x)
        {
            _list.Add(x);
        }

        /// <summary>
        /// Extend the list by appending all the items from the iterable.
        /// Similar to a[len(a):] = iterable.
        /// </summary>
        public void Extend(IEnumerable<T> iterable)
        {
            _list.AddRange(iterable);
        }

        /// <summary>
        /// Insert an item at a given position. The first argument is the
        /// index of the element before which to insert, so a.Insert(0, x)
        /// inserts at the front of the list, and a.Insert(Len(a), x) is
        /// equivalent to a.Append(x).
        /// </summary>
        public void Insert(int i, T x)
        {
            _list.Insert((int)_NormalizeIndex(i), x);
        }

        /// <summary>
        /// Remove the first item from the list whose value is equal to x. It
        /// raises a ValueError if there is no such item.
        /// </summary>
        public void Remove(T x)
        {
            if (!_list.Remove(x))
            {
                throw new ValueError($"{x} not in list");
            }
        }

        /// <summary>
        /// Remove the item at the given position in the list, and return it.
        /// If no index is specified, a.Pop() removes and returns the last
        /// item in the list. It raises an IndexError if the list is empty or
        /// the index is outside the list range.
        /// </summary>
        public T Pop(int i = -1)
        {
            if (Len() == 0)
            {
                throw new IndexError("pop from empty list");
            }

            try
            {
                _NormalizeIndex(i);
            }
            catch (IndexError)
            {
                throw new IndexError($"pop index {i} out of range");
            }

            var item = _list[i];
            _list.RemoveAt(i);

            return item;
        }

        /// <summary>
        /// Remove all items from the list.
        /// </summary>
        public void Clear()
        {
            _list.Clear();
        }

        /// <summary>
        /// Returns whether the item is in the list.
        /// </summary>
        public bool Contains(T x)
        {
            return _list.Contains(x);
        }

        /// <summary>
        /// Return zero-based index in the list of the first item whose value
        /// is equal to x. Raises a ValueError if there is no such item.
        /// The optional arguments start and end are interpreted as in the
        /// slice notation and are used to limit the search to a particular
        /// subsequence of the list. The returned index is computed relative
        /// to the beginning of the full sequence rather than the start
        /// argument.
        /// </summary>
        public uint Index(T x, int start = 0, int end = -1)
        {
            start = (int)_NormalizeIndex(start);
            int count = (int)_NormalizeIndex(end) - start;

            var result = _list.IndexOf(x, start, count);

            if (result == -1)
            {
                throw new ValueError($"{x} is not in list");
            }

            return (uint)result;
        }

        /// <summary>
        /// Return the number of times x appears in the list.
        /// </summary>
        public uint Count(T x)
        {
            return (uint)_list.Count(y => x.Equals(y));
        }

        /// <summary>
        /// Returns the number of items in the list.
        /// </summary>
        public uint Len()
        {
            return (uint)_list.Count;
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
        /// Reverse the elements of the list in place.
        /// </summary>
        public void Reverse()
        {
            _list.Reverse();
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
        /// Returns the item at the given index in the list.
        /// </summary>
        public T this[int index]
        {
            get
            {
                index = (int)_NormalizeIndex(index);
                return _list[index];
            }
            set
            {
                index = (int)_NormalizeIndex(index);
                _list[index] = value;
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
        }

        /// <summary>
        /// Creates a shallow copy this list as a .NET list.
        /// </summary>
        public System.Collections.Generic.List<T> ToList()
        {
            return _list[..(int)Len()];
        }

        /// <summary>
        /// Returns a reference to the underlying list.
        /// </summary>
        public unsafe System.Collections.Generic.List<T> AsList()
        {
            return _list;
        }

        public static bool operator ==(List<T> left, List<T> right)
        {
            return left._list == right._list;
        }

        public static bool operator !=(List<T> left, List<T> right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            if (obj is List<T> other)
            {
                return this == other;
            }

            return false;
        }

        public override int GetHashCode()
        {
            // Wrap overflows
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + typeof(List<T>).GetHashCode();
                hash = hash * 23 + _list.GetHashCode();

                return hash;
            }
        }

        public override string Repr()
        {
            var joinedItems = string.Join(", ", _list);

            return $"[{joinedItems}]";
        }

        private (uint, uint) _NormalizeSlice(int start, int end, bool forInsertion = false)
        {
            return (_NormalizeIndex(start, forInsertion), _NormalizeIndex(end, forInsertion));
        }

        private uint _NormalizeIndex(int i, bool forInsertion = false)
        {
            if (forInsertion)
            {
                return (uint)Math.Clamp(i, 0, (int)Len());
            }

            if (i > Len() || i < -Len())
            {
                throw new IndexError($"list index {i} out of range");
            }

            if (i < 0)
            {
                return (uint)((int)Len() + i);
            }

            return (uint)i;
        }

        private System.Collections.Generic.List<T> _list;
    }
}
