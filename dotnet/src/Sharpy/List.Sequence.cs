namespace Sharpy
{
    public sealed partial class List<T>
    {
        public T __GetItem__(int index)
        {
            index = (int)_NormalizeIndex(index);
            return _list[index];
        }

        /// <summary>
        /// Return the number of times x appears in the list.
        /// </summary>
        public uint Count(T x)
        {
            return (uint)_list.Count(y => x.Equals(y));
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
    }
}
