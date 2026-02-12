using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharpy
{
    using static Builtins;

    /// <summary>
    /// Python-style methods and deprecated dunder methods for List&lt;T&gt;.
    /// </summary>
    public sealed partial class List<T>
    {
        #region Python-style Methods

        /// <summary>
        /// Add an item to the end of the list. Similar to a[len(a):] = [x].
        /// </summary>
        public void Append(T x) => _list.Add(x);

        /// <summary>
        /// Extend the list by appending all the items from the iterable.
        /// Similar to a[len(a):] = iterable.
        /// </summary>
        public void Extend(IEnumerable<T> enumerable)
        {
            if (enumerable is null)
            {
                throw TypeError.ArgNone("extend", "enumerable");
            }

            _list.AddRange(enumerable);
        }

        /// <summary>
        /// Remove all items from the list.
        /// </summary>
        public void Clear() => _list.Clear();

        /// <summary>
        /// Insert an item at a given position. The first argument is the
        /// index of the element before which to insert, so a.Insert(0, x)
        /// inserts at the front of the list, and a.Insert(Len(a), x) is
        /// equivalent to a.Append(x).
        /// </summary>
        public void Insert(int i, T x)
        {
            _list.Insert(Sharpy.Index.Normalize(i, _list.Count, false, true), x);
        }

        /// <summary>
        /// Remove the item at the given position in the list, and return it.
        /// If no index is specified, a.Pop() removes and returns the last
        /// item in the list. It raises an IndexError if the list is empty or
        /// the index is outside the list range.
        /// </summary>
        public T Pop(int i = -1)
        {
            if (_list.Count == 0)
            {
                throw new IndexError("pop from empty list");
            }

            try
            {
                i = Sharpy.Index.Normalize(i, _list.Count, false, false);
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
        /// Reverse the elements of the list in place.
        /// </summary>
        public void Reverse() => _list.Reverse();

        /// <summary>
        /// Return the number of times x appears in the list.
        /// </summary>
        public uint Count(T x)
        {
            if (x is null)
            {
                return (uint)_list.Count(y => y is null);
            }

            return (uint)_list.Count(y => x.Equals(y));
        }

        /// <summary>
        /// Return zero-based index in the list of the first item whose value
        /// is equal to x. Raises a <see cref="ValueError"/> if there is no
        /// such item.
        /// </summary>
        /// <remarks>
        /// The optional arguments start and end are interpreted as
        /// in the slice notation and are used to limit the search to a
        /// particular subsequence of the list. The returned index is computed
        /// relative to the beginning of the full sequence rather than the
        /// start argument.
        /// </remarks>
        public uint Index(T x, int start = 0, int end = -1)
        {
            int count;

            try
            {
                start = Sharpy.Index.Normalize(start, _list.Count, false, false);
                count = Sharpy.Index.Normalize(end, _list.Count, false, false) - start;
            }
            catch (IndexError)
            {
                throw new ValueError($"{x} is not in list");
            }

            var result = _list.IndexOf(x, start, count);

            if (result == -1)
            {
                throw new ValueError($"{x} is not in list");
            }

            return (uint)result;
        }

        /// <summary>
        /// Returns whether the item is in the list.
        /// </summary>
        public bool Contains(T x) => _list.Contains(x);

        #endregion

        #region String Representation

        /// <summary>
        /// Returns a string representation of this list.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append('[');

            int i = 1;
            var numElems = _list.Count;

            foreach (var item in _list)
            {
                builder.Append(Repr(item));

                if (i < numElems)
                {
                    builder.Append(", ");
                }

                ++i;
            }

            builder.Append(']');

            return builder.ToString();
        }

        #endregion

        #region Deprecated Dunder Methods

        /// <summary>
        /// Returns the number of items in the list.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use the Count property via IReadOnlyCollection{T} interface instead.
        /// Note: For counting occurrences of a specific item, use <c>list.Count(item)</c> method.
        /// </remarks>
        public int __Len__() => _list.Count;

        /// <summary>
        /// Returns whether the item is in the list.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <see cref="Contains(T)"/> instead.
        /// </remarks>
        public bool __Contains__(T x) => Contains(x);

        /// <summary>
        /// Returns an iterator over the list elements.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <see cref="GetEnumerator()"/> instead.
        /// </remarks>
        public Iterator<T> __Iter__() => (Iterator<T>)GetEnumerator();

        /// <summary>
        /// Returns a reverse iterator over the list.
        /// </summary>
        public Iterator<T> __Reversed__() => new ListReverseIterator<T>(this);

        /// <summary>
        /// Deprecated: Use <see cref="GetHashCode()"/> instead.
        /// </summary>
        public int __Hash__() => GetHashCode();

        /// <summary>
        /// Deprecated: Use <see cref="ToString()"/> instead.
        /// </summary>
        public string __Repr__() => ToString();

        /// <summary>
        /// Concatenates this list with another list, returning a new list.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>list1 + list2</c> operator instead.
        /// </remarks>
        public List<T> __Add__(List<T> other) => this + other;

        /// <summary>
        /// Concatenates this list with another enumerable, returning a new list.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>list1 + list2</c> operator instead.
        /// </remarks>
        public List<T> __Add__(IEnumerable<T> other)
        {
            var res = new List<T>();
            res._list.AddRange(_list);
            res._list.AddRange(other);

            return res;
        }

        /// <summary>
        /// Right-side addition (prepends other to this list).
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>list1 + list2</c> operator instead.
        /// </remarks>
        public List<T> __RAdd__(List<T> other)
        {
            if (other is null)
            {
                throw TypeError.CanOnlyNot("concatenate", $"List<{typeof(T).Name}>", "NoneType", "to", $"List<{typeof(T).Name}>");
            }

            var res = other.Copy();
            res.Extend(this);

            return res;
        }

        /// <summary>
        /// Right-side addition (prepends other to this list).
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>list1 + list2</c> operator instead.
        /// </remarks>
        public List<T> __RAdd__(IEnumerable<T> other)
        {
            var res = new List<T>();
            res._list.AddRange(other);
            res._list.AddRange(_list);

            return res;
        }

        /// <summary>
        /// In-place addition (extend) from a list.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>list.Extend(other)</c> instead.
        /// </remarks>
        public void __IAdd__(List<T> other)
        {
            if (other is null)
            {
                throw TypeError.CanOnlyNot("concatenate", $"List<{typeof(T).Name}>", "NoneType", "to", $"List<{typeof(T).Name}>");
            }

            Extend(other);
        }

        /// <summary>
        /// In-place addition (extend) from an enumerable.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>list.Extend(other)</c> instead.
        /// </remarks>
        public void __IAdd__(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            _list.AddRange(other);
        }

        /// <summary>
        /// Repeats this list a specified number of times, returning a new list.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>list * count</c> operator instead.
        /// </remarks>
        public List<T> __Mul__(int count) => this * count;

        /// <summary>
        /// Repeats this list a specified number of times, returning a new list.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>count * list</c> operator instead.
        /// </remarks>
        public List<T> __RMul__(int count) => count * this;

        /// <summary>
        /// In-place repetition of this list.
        /// </summary>
        public void __IMul__(int i)
        {
            if (i <= 0)
            {
                Clear();

                return;
            }

            var originalLength = _list.Count;

            --i;

            for (; i > 0; --i)
            {
                for (uint j = 0; j < originalLength; ++j)
                {
                    _list.Add(_list[(int)j]);
                }
            }
        }

        #endregion
    }
}
