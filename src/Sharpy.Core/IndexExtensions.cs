namespace Sharpy
{
    public static class IndexExtensions
    {
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
