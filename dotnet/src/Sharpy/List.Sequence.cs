namespace Sharpy
{
    public sealed partial class List<T>
    {
        public T __GetItem__(int index)
        {
            index = (int)Sharpy.Index.Normalize(index, (uint)_list.Count, false, false);
            return _list[index];
        }

        public List<T> __GetItem__(Slice slice)
        {
            if (slice.step == 0)
            {
                throw new ValueError("slice step cannot be zero");
            }

            if (slice.step < 0)
            {
                return [];
            }

            (int start, int end) = ((int, int))Slice.Normalize(slice.start, slice.end, (uint)_list.Count);

            return new List<T>
            {
                _list = [.. _list.Skip(start).Take(end - start).Where((item, index) => index % slice.step == 0)]
            };
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
                start = (int)Sharpy.Index.Normalize(start, (uint)_list.Count, false, false);
                count = (int)Sharpy.Index.Normalize(end, (uint)_list.Count, false, false) - start;
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
