namespace Sharpy
{
    public readonly partial struct Str
    {
        public Str __GetItem__(int index)
        {
            index = (int)Sharpy.Index.Normalize(index, (uint)_s.Length, false, false);
            return new Str(_s[index]);
        }

        public Str __GetItem__(Slice slice)
        {
            if (slice.step == 0)
            {
                throw new ValueError("slice step cannot be zero");
            }

            if (slice.step < 0)
            {
                return "";
            }

            (int start, int end) = ((int, int))Slice.Normalize(slice.start, slice.end, (uint)_s.Length);

            return "TODO";
        }

        /// <summary>
        /// Return the number of times x appears in the list.
        /// </summary>
        public uint Count(Str x)
        {
            return (uint)_s.Count(y => x.Equals(y));
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
        public uint Index(char x, int start = 0, int end = -1)
        {
            int count;

            try
            {
                start = (int)Sharpy.Index.Normalize(start, (uint)_s.Length, false, false);
                count = (int)Sharpy.Index.Normalize(end, (uint)_s.Length, false, false) - start;
            }
            catch (IndexError)
            {
                throw new ValueError($"{x} is not in list");
            }

            var result = _s.IndexOf(x, start, count);

            if (result == -1)
            {
                throw new ValueError($"{x} is not in list");
            }

            return (uint)result;
        }
    }
}
