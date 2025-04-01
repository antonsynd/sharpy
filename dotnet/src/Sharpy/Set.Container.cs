namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool Contains(T x)
        {
            return __Contains__(x);
        }

        public bool __Contains__(T x)
        {
            return _set.Contains(x);
        }
    }
}
