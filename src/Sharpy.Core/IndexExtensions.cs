namespace Sharpy.Core
{
    public static class IndexExtensions
    {
        public static int ToNormalizedInt32(this System.Index index, int max, bool forSlice, bool forInsertion)
        {
            if (index.IsFromEnd)
            {
                return Sharpy.Core.Index.Normalize(-index.Value, max, forSlice, forInsertion);
            }

            return Sharpy.Core.Index.Normalize(index.Value, max, forSlice, forInsertion);
        }
    }
}
