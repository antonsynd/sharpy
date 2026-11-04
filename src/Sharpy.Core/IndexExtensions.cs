namespace Sharpy
{
    /// <summary>Extension methods for System.Index to normalize indices.</summary>
    public static class IndexExtensions
    {
        /// <summary>Convert a System.Index to a normalized int, handling from-end indices.</summary>
        public static int ToNormalizedInt32(this System.Index index, int max, bool forSlice, bool forInsertion)
        {
            int i = index.IsFromEnd ? -index.Value : index.Value;
            return forSlice || forInsertion ? Index.Clamp(i, max) : Index.Normalize(i, max);
        }
    }
}
