namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool __Ge__(Set<T> other)
        {
            return __Eq__(other) || !__Lt__(other);
        }

        public bool __Ge__(Collections.Interfaces.Set<T> other)
        {
            return __Eq__(other) || !__Lt__(other);
        }
    }
}
