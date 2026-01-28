namespace Sharpy.Core
{
    public static class RangeExtensions
    {
        public static Slice ToSlice(this System.Range range, int max, bool forInsertion)
        {
            return new Slice(range.Start.ToNormalizedInt32(max, true, forInsertion), range.End.ToNormalizedInt32(max, true, forInsertion));
        }
    }
}
