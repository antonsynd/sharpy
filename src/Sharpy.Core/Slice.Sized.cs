namespace Sharpy
{
    public readonly partial struct Slice
    {
        public int __Len__()
        {
            return Len(start, end, step);
        }
    }
}
