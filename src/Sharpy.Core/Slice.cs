namespace Sharpy
{
    /// <summary>
    /// Represents a slice with start, end, and step, matching Python's slice semantics.
    /// </summary>
    public readonly partial struct Slice
    {
        /// <summary>The start index of the slice.</summary>
        public readonly int start;
        /// <summary>The end index of the slice (exclusive).</summary>
        public readonly int end;
        /// <summary>The step of the slice.</summary>
        public readonly int step;

        /// <summary>Create a slice with the given start, end, and step.</summary>
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

        /// <summary>Compute the number of elements in a slice range.</summary>
        public static int Len(int start, int end, int step)
        {
            System.Diagnostics.Debug.Assert(step > 0, "Slice.Len requires a positive step");
            // Ceiling division: ⌈(end - start) / step⌉
            var length = end - start;
            return (length + step - 1) / step;
        }

        /// <summary>Create a Slice from a System.Range.</summary>
        public static Slice FromRange(System.Range range)
        {
            return new Slice(range.Start.Value, range.End.Value);
        }

        internal static (int, int) Normalize(int start, int end, int max)
        {
            return (Index.Normalize(start, max, true, false), Index.Normalize(end, max, true, false));
        }

        /// <summary>
        /// Normalizes start and end indices for a negative step, matching Python's
        /// <c>slice.indices(length)</c> algorithm.
        /// </summary>
        /// <param name="rawStart">The raw start value from the Slice (already defaulted).</param>
        /// <param name="rawEnd">The raw end value from the Slice (already defaulted).</param>
        /// <param name="step">The step value (must be negative).</param>
        /// <param name="length">The length of the sequence being sliced.</param>
        /// <returns>Normalized (start, end) where iteration should go from start down to end (exclusive).</returns>
        private static (int, int) NormalizeForNegativeStep(int rawStart, int rawEnd, int step, int length)
        {
            // For negative step, start defaults to length-1 and end defaults to
            // "before index 0" (represented as -1 after normalization).
            // Clamp both to [-1, length-1].
            int nStart = rawStart;
            if (nStart < 0)
                nStart = length + nStart;
            if (nStart >= length)
                nStart = length - 1;
            if (nStart < -1)
                nStart = -1;

            int nEnd = rawEnd;
            if (nEnd < -length)
                nEnd = -1;
            else if (nEnd < 0)
                nEnd = length + nEnd;
            if (nEnd >= length)
                nEnd = length - 1;

            return (nStart, nEnd);
        }

        /// <summary>
        /// Slice a Sharpy.List&lt;T&gt; using Python slice semantics.
        /// Used by generated code for list[start:stop:step] expressions.
        /// </summary>
        public static List<T> GetSlice<T>(
            List<T> list, int? start, int? end, int? step)
        {
            var s = new Slice(start, end, step);
            if (s.step == 0)
                throw new ValueError("slice step cannot be zero");

            int count = ((System.Collections.Generic.ICollection<T>)list).Count;
            var result = new List<T>();

            if (s.step < 0)
            {
                var (nStart, nEnd) = NormalizeForNegativeStep(s.start, s.end, s.step, count);
                for (int i = nStart; i > nEnd; i += s.step)
                    result.Add(list[i]);
            }
            else
            {
                var (nStart, nEnd) = Normalize(s.start, s.end, count);
                for (int i = nStart; i < nEnd; i += s.step)
                    result.Add(list[i]);
            }

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
            {
                var (nStart, nEnd) = NormalizeForNegativeStep(s.start, s.end, s.step, str.Length);
                if (nStart <= nEnd)
                    return "";

                var sb = new System.Text.StringBuilder();
                for (int i = nStart; i > nEnd; i += s.step)
                    sb.Append(str[i]);
                return sb.ToString();
            }
            else
            {
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

        /// <summary>
        /// Slice a Bytes using Python slice semantics.
        /// Used by generated code for bytes[start:stop:step] expressions.
        /// </summary>
        public static Bytes GetSlice(Bytes bytes, int? start, int? end, int? step)
        {
            return bytes.Slice(start, end, step);
        }
    }
}
