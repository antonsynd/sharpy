namespace Sharpy
{
    public readonly partial struct Slice
    {
        public readonly int start;
        public readonly int end;
        public readonly int step;

        public Slice(int start, int end, int step = 1)
        {
            this.start = start;
            this.end = end;
            this.step = step;
        }

        /// <summary>
        /// Constructs a Slice from nullable parameters, matching Python's slice semantics.
        /// Omitted (null) bounds use defaults based on step direction.
        /// </summary>
        public Slice(int? start, int? end, int? step = null)
        {
            this.step = step ?? 1;
            this.start = start ?? (this.step > 0 ? 0 : -1);
            this.end = end ?? (this.step > 0 ? int.MaxValue : int.MinValue);
        }

        public static int Len(int start, int end, int step)
        {
            // Efficient ceil division (from ChatGPT)
            var length = end - start;
            return (length + step - 1) / step;
        }

        public static Slice FromRange(System.Range range)
        {
            return new Slice(range.Start.Value, range.End.Value);
        }

        internal static (int, int) Normalize(int start, int end, int max)
        {
            return (Index.Normalize(start, max, true, false), Index.Normalize(end, max, true, false));
        }

        /// <summary>
        /// Slice a System.Collections.Generic.List&lt;T&gt; using Python slice semantics.
        /// Used by generated code for list[start:stop:step] expressions.
        /// </summary>
        public static System.Collections.Generic.List<T> GetSlice<T>(
            System.Collections.Generic.List<T> list, int? start, int? end, int? step)
        {
            var s = new Slice(start, end, step);
            if (s.step == 0)
                throw new ValueError("slice step cannot be zero");
            if (s.step < 0)
                return new System.Collections.Generic.List<T>();

            var (nStart, nEnd) = Normalize(s.start, s.end, list.Count);
            var result = new System.Collections.Generic.List<T>();
            for (int i = nStart; i < nEnd; i += s.step)
                result.Add(list[i]);
            return result;
        }

        /// <summary>
        /// Slice a string using Python slice semantics.
        /// Used by generated code for str[start:stop:step] expressions.
        /// </summary>
        public static string GetSlice(string str, int? start, int? end, int? step)
        {
            var s = new Slice(start, end, step);
            if (s.step == 0)
                throw new ValueError("slice step cannot be zero");
            if (s.step < 0)
                return "";

            var (nStart, nEnd) = Normalize(s.start, s.end, str.Length);
            if (nStart >= nEnd)
                return "";

            if (s.step == 1)
                return str.Substring(nStart, nEnd - nStart);

            var sb = new System.Text.StringBuilder();
            for (int i = nStart; i < nEnd; i += s.step)
                sb.Append(str[i]);
            return sb.ToString();
        }
    }
}
