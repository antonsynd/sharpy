namespace Sharpy
{
    /// <summary>Extension methods for System.Index to normalize indices.</summary>
    public static class IndexExtensions
    {
        /// <summary>Convert a System.Index to a normalized int, handling from-end indices.</summary>
        public static int ToNormalizedInt32(this System.Index index, int max, bool forSlice, bool forInsertion)
        {
            if (index.IsFromEnd)
            {
                return Index.Normalize(-index.Value, max, forSlice, forInsertion);
            }

            return Index.Normalize(index.Value, max, forSlice, forInsertion);
        }
    }
}
