namespace Sharpy {
    public struct Slice(int start, int end, int step = 1)
    {
        public readonly int start = start;
        public readonly int end = end;
        public readonly int step = step;
    }
}
