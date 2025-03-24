namespace Sharpy
{
    public readonly partial struct Slice
    {
        public uint __Len__()
        {
            return Len(start, end, step);
        }
    }
}
