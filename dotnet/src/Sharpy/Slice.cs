namespace Sharpy
{
    public readonly struct Slice(int start, int end, int step = 1)
    {
        public readonly int start = start;
        public readonly int end = end;
        public readonly int step = step;

        public uint __Len__()
        {
            return Len(start, end, step);
        }

        public static uint Len(int start, int end, int step)
        {
            // Efficient ceil division (from ChatGPT)
            var length = end - start;
            return (uint)((length + step - 1) / step);
        }

        internal static (uint, uint) Normalize(int start, int end, uint max)
        {
            return (Index.Normalize(start, max, true, false), Index.Normalize(end, max, true, false));
        }
    }
}
