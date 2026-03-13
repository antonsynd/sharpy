namespace Sharpy
{
    /// <summary>Extension methods for System.Range to convert to Slice.</summary>
    public static class RangeExtensions
    {
        /// <summary>Convert a System.Range to a Slice, normalizing indices against the given max length.</summary>
        public static Slice ToSlice(this System.Range range, int max, bool forInsertion)
        {
            return new Slice(range.Start.ToNormalizedInt32(max, true, forInsertion), range.End.ToNormalizedInt32(max, true, forInsertion));
        }
    }
}
