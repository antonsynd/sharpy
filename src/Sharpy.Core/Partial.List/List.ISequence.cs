using System.Collections.Generic;
using System.Linq;
namespace Sharpy.Core
{
    public sealed partial class List<T>
    {
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
        public List<T> __RAdd__(IEnumerable<T> other)
        {
            var res = new List<T>();
            res._list.AddRange(other);
            res._list.AddRange(_list);

            return res;
        }

        /// <summary>
        /// Returns the element at the specified index.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use the indexer <c>list[index]</c> instead.
        /// </remarks>
        public T __GetItem__(int index)
        {
            return this[index];
        }

        /// <summary>
        /// Returns a slice of the list.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use the indexer <c>list[start, end]</c> or <c>list[start, end, step]</c> instead.
        /// </remarks>
        public List<T> __GetItem__(Slice slice)
        {
            if (slice.step == 0)
            {
                throw new ValueError("slice step cannot be zero");
            }

            if (slice.step < 0)
            {
                return new List<T>();
            }

            (int start, int end) = Slice.Normalize(slice.start, slice.end, _list.Count);

            var res = new List<T>();
            res._list.AddRange(_list.Skip(start).Take(end - start).Where((item, index) => index % slice.step == 0));
            return res;
        }

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
                start = Sharpy.Core.Index.Normalize(start, _list.Count, false, false);
                count = Sharpy.Core.Index.Normalize(end, _list.Count, false, false) - start;
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
    }
}
