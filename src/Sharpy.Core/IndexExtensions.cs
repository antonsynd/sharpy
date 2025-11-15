namespace Sharpy.Core;

public static class IndexExtensions
{
    public static uint ToNormalizedUint32(this System.Index index, uint max, bool forSlice, bool forInsertion)
    {
        if (index.IsFromEnd)
        {
            return Sharpy.Core.Index.Normalize(-index.Value, max, forSlice, forInsertion);
        }

        return Sharpy.Core.Index.Normalize(index.Value, max, forSlice, forInsertion);
    }
}
