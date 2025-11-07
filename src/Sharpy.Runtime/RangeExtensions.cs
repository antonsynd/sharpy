namespace Sharpy;

public static class RangeExtensions
{
    public static Slice ToSlice(this System.Range range, uint max, bool forInsertion)
    {
        return new Slice((int)range.Start.ToNormalizedUint32(max, true, forInsertion), (int)range.End.ToNormalizedUint32(max, true, forInsertion));
    }
}
